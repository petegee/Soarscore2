// kanban/in-progress/lane-assignment.md WI-8 — step definitions for
// Features/AssigningSpots.feature: the Spot glossary sentence end to end —
// the CD assigns (or re-assigns) one drawn group's complete field-spot
// mapping, the capture-time recording view reads it back spot-ordered, a
// rejected draw discards it, a withdrawn competitor cannot be named, and
// score capture runs unchanged on a group with none (the NFR-4 guard).
//
// The Givens are NOT redefined here: they are AcceptingTheDrawSteps' shared
// draw-acceptance steps, composed verbatim into the feature per the story's
// finding 8, with the competition they create travelling across the two
// Binding classes via DrawAcceptanceState (context injection — see that
// file's header for why that bridge must exist). Reject, redraw and withdraw
// likewise reuse AcceptingTheDrawSteps' own When steps.
//
// The capture steps below are worded apart from CapturingAScoreSteps'
// bindings ("in the drawn group", "the entry's flight", "a flight time of")
// for the same reason every Steps class rewords its neighbours: Reqnroll
// binds step regexes assembly-wide, so a regex shared verbatim across two
// Binding classes is an ambiguous match. Scenario 5 deliberately drives the
// EXISTING capture path (open entry → open flight → capture-measurement)
// against an unassigned group — no spot may gate or alter it (NFR-4).
//
// F5J (30-f5j) throughout, via the shared Givens: literal MinPerGroup 6 makes
// the 6-pilot field draw to exactly one group, so "the group" is unambiguous —
// round 1, task-round 1, group 1 — and every step re-resolves its GroupId
// FRESH from GET /competition (never cached), which is what lets scenario 3's
// redraw be seen as the fresh, unassigned group it is.
//
// One instance of this class per scenario (Reqnroll's default binding
// lifetime); the fields below are scenario-scoped, DrawAcceptanceState is the
// only cross-class state.

using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class AssigningSpotsSteps
{
    private static HttpClient Client => AcceptanceFixture.Client;

    private readonly DrawAcceptanceState _state;

    // The spot coordinates are fixed by the shared Givens' composition: one
    // drawn round, F5J's single task per round, one group of six.
    private const int PhaseOrdinal = 0; // Phases.Length at draw time (see CapturingAScoreSteps)
    private const int RoundOrdinal = 1; // rounds and task-rounds are 1-based
    private const int TaskRoundOrdinal = 1;

    private EntryId _entryId;
    private HttpResponseMessage? _rawResponse;

    // Set by every successful assignment, read by the fresh-group Then: the
    // redraw must mint a new GroupId, not resurrect the assigned one.
    private GroupId? _lastAssignedGroupId;

    public AssigningSpotsSteps(DrawAcceptanceState state) => _state = state;

    // ----------------------------------------------------------------- When

    [When(@"^the contest director assigns the group's field spots ([0-9, ]+)$")]
    public async Task WhenTheContestDirectorAssignsTheGroupsFieldSpots(string csvSpots) =>
        await AssignAsync(Parse(csvSpots));

    // Whole-replacement semantics (D3) — same command, second mapping wins.
    // A separate wording keeps the feature's "Re-assigning" scenario readable.
    [When(@"^the contest director re-assigns the group's field spots ([0-9, ]+)$")]
    public async Task WhenTheContestDirectorReAssignsTheGroupsFieldSpots(string csvSpots) =>
        await AssignAsync(Parse(csvSpots));

    // The refusal variant: the raw response is held for its Then to inspect,
    // mirroring the "tries to reject" pair in AcceptingTheDrawSteps.
    [When(@"^the contest director tries to assign the group's field spots ([0-9, ]+)$")]
    public async Task WhenTheContestDirectorTriesToAssignTheGroupsFieldSpots(string csvSpots)
    {
        var (groupId, spots) = await BuildAssignmentAsync(Parse(csvSpots));
        _rawResponse = await ApiClient.PostCommandRawAsync(
            Client, "/assign-group-spots", new AssignGroupSpots(_state.CompetitionId, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal, groupId, spots));
    }

    [When(@"^the scorer opens an entry for competitor (\d+) in the drawn group$")]
    public async Task WhenTheScorerOpensAnEntryForCompetitorInTheDrawnGroup(int competitorOrdinal)
    {
        var groupId = await ResolveGroupIdAsync();
        _entryId = await ApiClient.PostCommandAsync<EntryId>(
            Client, "/open-entry",
            new OpenEntry(_state.CompetitionId, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal, groupId, Competitor(competitorOrdinal)));
    }

    [When(@"^the scorer opens the entry's flight$")]
    public async Task WhenTheScorerOpensTheEntrysFlight() =>
        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(_entryId));

    [When(@"^the scorer captures a flight time of (\d+) seconds$")]
    public async Task WhenTheScorerCapturesAFlightTimeOfSeconds(int seconds) =>
        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/capture-measurement",
            new CaptureMeasurement(_entryId, 1, "flightTime", MeasuredValue.Of((decimal)seconds)));

    // ----------------------------------------------------------------- Then

    // The view states the assignment AS RECORDED, ordered by spot (WI-5): the
    // command's order and the view's order deliberately differ in scenario 1,
    // so this Then pins both halves — each spot names the right competitor,
    // and the sequence is spot-ordered whatever order the command gave.
    [Then(@"^the recording view shows the group's field spots ([0-9, ]+) held by competitors ([0-9, ]+) in spot order$")]
    public async Task ThenTheRecordingViewShowsTheFieldSpotsInSpotOrder(string csvSpots, string csvCompetitors)
    {
        var spots = Parse(csvSpots);
        var competitors = Parse(csvCompetitors);

        var group = await RecordingGroupAsync();

        group.Spots.Select(s => s.Spot).Should().Equal(spots);
        group.Spots.Select(s => s.CompetitorRef).Should().Equal(competitors.Select(Competitor));
    }

    // Unassigned is a fact, not a gap (D2): empty means unassigned, and no
    // spot may be inferred from the draw's sequence position to fill it.
    [Then(@"^the recording view shows the group with no field spots assigned$")]
    public async Task ThenTheRecordingViewShowsTheGroupWithNoFieldSpotsAssigned()
    {
        var group = await RecordingGroupAsync();

        group.Spots.Should().BeEmpty();
    }

    // The discard made concrete: the redraw mints fresh GroupIds (D2/D3), so
    // the group now standing where the assignment was is not the one the
    // spots were assigned to — the old assignment died with the old phase.
    [Then(@"^the recording view shows a fresh, unassigned group where the assignment was$")]
    public async Task ThenTheRecordingViewShowsAFreshUnassignedGroupWhereTheAssignmentWas()
    {
        _lastAssignedGroupId.HasValue.Should().BeTrue("a spot assignment must have been made earlier in the scenario");

        var group = await RecordingGroupAsync();

        group.GroupRef.Should().NotBe(_lastAssignedGroupId!.Value);
    }

    [Then(@"^the assignment is refused because a withdrawn competitor is not a live member of the group$")]
    public async Task ThenTheAssignmentIsRefusedBecauseAWithdrawnCompetitorIsNotALiveMemberOfTheGroup()
    {
        _rawResponse!.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await ReadProblemTitleAsync(_rawResponse)).Should().Be("assignSpots.competitorNotInGroup");
    }

    // Scenario 5's NFR-4 guard, first half: the EXISTING capture path stored
    // the flight on the unassigned group's entry, unchanged by spots.
    [Then(@"^the captured flight reads back with a flight time of (\d+) seconds$")]
    public async Task ThenTheCapturedFlightReadsBackWithAFlightTimeOfSeconds(int seconds)
    {
        var entry = await EntryReader.LoadAsync(AcceptanceFixture.EventStore, _entryId, TestContext.Current.CancellationToken);

        entry.Flights.Should().ContainSingle();
        entry.Flights[0].Measurements.Should().ContainSingle(m => m.Metric == "flightTime");
        entry.Flights[0].Measurements[0].Value.Should().Be(MeasuredValue.Of((decimal)seconds));
    }

    // Second half: the recording view's own machinery still reads the group —
    // the capture shows in the usual bucketing while Spots stays the empty
    // unassigned fact (the paired Then in the feature asserts that).
    [Then(@"^the recording view shows competitor (\d+) as recorded$")]
    public async Task ThenTheRecordingViewShowsCompetitorAsRecorded(int competitorOrdinal)
    {
        var group = await RecordingGroupAsync();

        group.NotRecordedCompetitorRefs.Should().NotContain(Competitor(competitorOrdinal));
    }

    // ------------------------------------------------------------ helpers

    private async Task AssignAsync(IReadOnlyList<int> spots)
    {
        var (groupId, payload) = await BuildAssignmentAsync(spots);

        var assigned = await ApiClient.PostCommandAsync<GroupId>(
            Client, "/assign-group-spots",
            new AssignGroupSpots(_state.CompetitionId, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal, groupId, payload));

        assigned.Should().Be(groupId); // ICommand<GroupId>: names the group it assigned

        _lastAssignedGroupId = groupId;
    }

    // One command assigns the COMPLETE mapping for the group (D4): the i-th
    // spot number of the step's list goes to the i-th registered competitor,
    // covering all six live members with distinct positive spots. The group
    // id is resolved fresh so the command always names the group that stands
    // NOW (scenario 3's redraw mints new ones).
    private async Task<(GroupId GroupId, IReadOnlyList<GroupSpot> Spots)> BuildAssignmentAsync(IReadOnlyList<int> spots)
    {
        var groupId = await ResolveGroupIdAsync();
        var payload = spots.Select((spot, i) => new GroupSpot(_state.Competitors[i], spot)).ToList();
        return (groupId, payload);
    }

    private CompetitorId Competitor(int ordinal) => _state.Competitors[ordinal - 1];

    private static IReadOnlyList<int> Parse(string csv) =>

        // Reqnroll's default binding culture is en-US, but the parse is
        // explicit so the step text's list never depends on host culture.
        csv.Split(',')
            .Select(s => int.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

    private async Task<GroupId> ResolveGroupIdAsync()
    {
        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_state.CompetitionId.Value}");

        return view.Competition.Phases.Single()
            .Rounds.Single(r => r.Ordinal == RoundOrdinal)
            .TaskRounds.Single()
            .Groups.Single(g => g.Ordinal == 1).Id;
    }

    private async Task<GroupRecordingView> RecordingGroupAsync()
    {
        var recording = await ApiClient.GetAsync<TaskRoundRecordingView>(
            Client,
            $"/task-round-recording?competitionRef={_state.CompetitionId.Value}&phaseOrdinal={PhaseOrdinal}&roundOrdinal={RoundOrdinal}&taskRoundOrdinal={TaskRoundOrdinal}");

        return recording.Groups.Should().ContainSingle().Subject;
    }

    private static async Task<string> ReadProblemTitleAsync(HttpResponseMessage response)
    {
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("title").GetString()!;
    }
}
