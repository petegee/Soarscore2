// kanban/completed/entry-completeness-indicator.md WI-5 — step definitions
// for Features/SeeingWhatIsRecorded.feature. Every step drives real HTTP
// against the real Soarscore.Api (AcceptanceFixture.Client), the discipline
// CapturingAScoreSteps.cs established and every later Steps class follows.
//
// A self-contained [Binding] class with its own Given/When/Then phrasing:
// Reqnroll binds step regexes assembly-wide, so phrasing shared verbatim with
// another Binding class is an ambiguous match — hence "an F5J competition
// under way with" here against ClosingACompetitionSteps's "an F5J competition
// is under way, with".
//
// F5J (30-f5j) throughout: literal MinPerGroup 6 makes a 6-pilot field draw to
// exactly one group, and its task declares six metrics, so a flight captured
// with flight time alone leaves a concrete five-metric gap in the task's
// declared order (startHeight, startHeightRecorded, landingDistance,
// overflySeconds, touchedByCompetitor).
//
// Every Then asserts facts about what is recorded — "recorded", "not
// recorded", "missing" — never completeness. That phrasing discipline is this
// feature's whole point (entry-completeness-indicator.md, design constraints).

using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class SeeingWhatIsRecordedSteps
{
    private static HttpClient Client => AcceptanceFixture.Client;

    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;

    private CompetitionId _competitionId;
    private GroupId _groupRef;
    private List<CompetitorId> _competitors = [];

    private CompetitorId _neverEntered = default!;
    private CompetitorId _enteredNotFlown = default!;
    private CompetitorId _partiallyCaptured = default!;

    private TaskRoundRecordingView? _recording;

    // ---------------------------------------------------------------- Given

    [Given(@"^an F5J competition under way with (\d+) competitors and (\d+) drawn rounds$")]
    public async Task GivenAnF5JCompetitionUnderWay(int competitorCount, int rounds)
    {
        var contentHash = await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(F5JDefinition));

        var slug = Guid.NewGuid().ToString("N");
        _competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/create-competition",
            new CreateCompetition($"Recording Acceptance {slug}", "Taupo", new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 25), contentHash));

        for (var i = 0; i < competitorCount; i++)
        {
            var email = $"pilot-seeing-{slug}-{i}@example.com".ToLowerInvariant();
            var personId = await ApiClient.PostCommandAsync<PersonId>(
                Client, "/register-person", new RegisterPerson($"Pilot {i + 1}", new ContactDetails { Email = email }, null));
            var competitorId = await ApiClient.PostCommandAsync<CompetitorId>(
                Client, "/register-competitor", new RegisterCompetitor(_competitionId, personId));
            _competitors.Add(competitorId);
        }

        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/draw-phase", new DrawPhase(_competitionId, rounds));

        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var taskRound = view.Competition.Phases.Single().Rounds.Single(r => r.Ordinal == 1).TaskRounds.Single();
        _groupRef = taskRound.Groups.Single().Id;
    }

    [Given(@"^all six competitors have flown round (\d+)$")]
    public async Task GivenAllSixCompetitorsHaveFlownRound(int roundOrdinal)
    {
        foreach (var competitorRef in _competitors)
        {
            await CaptureFlightAsync(roundOrdinal, competitorRef, fullMetrics: true);
        }
    }

    [Given(@"^every competitor but two has flown round (\d+)$")]
    public async Task GivenEveryCompetitorButTwoHasFlownRound(int roundOrdinal)
    {
        _neverEntered = _competitors[^1];

        foreach (var competitorRef in _competitors.Take(_competitors.Count - 2))
        {
            await CaptureFlightAsync(roundOrdinal, competitorRef, fullMetrics: true);
        }
    }

    [Given(@"^one of those two opened an entry without flying it$")]
    public async Task GivenOneOfThoseTwoOpenedAnEntryWithoutFlyingIt()
    {
        // The second of the two non-flyers: their Entry exists, its flight list
        // never does.
        _enteredNotFlown = _competitors[^2];
        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/open-entry", new OpenEntry(_competitionId, 0, 1, 1, _groupRef, _enteredNotFlown));
    }

    [Given(@"^every competitor has flown round (\d+) except the last, whose flight was captured with its flight time alone$")]
    public async Task GivenEveryoneFlewExceptOnePartiallyCapturedFlight(int roundOrdinal)
    {
        _partiallyCaptured = _competitors[^1];

        foreach (var competitorRef in _competitors.Take(_competitors.Count - 1))
        {
            await CaptureFlightAsync(roundOrdinal, competitorRef, fullMetrics: true);
        }

        await CaptureFlightAsync(roundOrdinal, _partiallyCaptured, fullMetrics: false);
    }

    // ----------------------------------------------------------------- When

    [When(@"^the contest director asks what is recorded for round (\d+)$")]
    public async Task WhenTheContestDirectorAsksWhatIsRecorded(int roundOrdinal) =>
        _recording = await ApiClient.GetAsync<TaskRoundRecordingView>(
            Client,
            $"/task-round-recording?competitionRef={_competitionId.Value}&phaseOrdinal=0&roundOrdinal={roundOrdinal}&taskRoundOrdinal=1");

    // ----------------------------------------------------------------- Then

    [Then(@"^all six competitors are shown as recorded with no metric gaps$")]
    public void ThenAllSixAreRecordedWithNoGaps()
    {
        var group = TheGroup();

        group.ExpectedCompetitorRefs.Should().Equal(_competitors);
        group.NotRecordedCompetitorRefs.Should().BeEmpty();
        group.RecordedWithoutFlightCompetitorRefs.Should().BeEmpty();
        group.MetricGaps.Should().BeEmpty();
    }

    [Then(@"^the competitor who never entered is named as not recorded$")]
    public void ThenTheNeverEnteredCompetitorIsNamed()
    {
        var group = TheGroup();

        group.NotRecordedCompetitorRefs.Should().Equal([_neverEntered]);
    }

    [Then(@"^the entry without a flight is shown as recorded but unflown$")]
    public void ThenTheUnflownEntryIsShown()
    {
        var group = TheGroup();

        // Recorded (not among the not-recorded), but without a flight.
        group.NotRecordedCompetitorRefs.Should().NotContain(_enteredNotFlown);
        group.RecordedWithoutFlightCompetitorRefs.Should().Equal([_enteredNotFlown]);
        group.MetricGaps.Where(g => g.CompetitorRef == _enteredNotFlown).Should().BeEmpty();
    }

    [Then(@"^that flight is shown missing its five other metrics in the task's declared order$")]
    public void ThenThePartiallyCapturedFlightIsNamed()
    {
        var group = TheGroup();

        var gaps = group.MetricGaps.Single(g => g.CompetitorRef == _partiallyCaptured);
        var flightGaps = gaps.Flights.Should().ContainSingle().Subject;
        flightGaps.Sequence.Should().Be(1);
        flightGaps.MissingMetrics.Should().Equal(
        [
            "startHeight", "startHeightRecorded", "landingDistance", "overflySeconds", "touchedByCompetitor",
        ]);
    }

    // --------------------------------------------------------------- helpers

    private GroupRecordingView TheGroup() =>
        _recording!.Groups.Should().ContainSingle().Subject;

    /// <summary>
    /// Opens the Entry and its one flight over HTTP, capturing either all six
    /// F5J metrics or flightTime alone. Flight times are irrelevant here —
    /// nothing below scores anything.
    /// </summary>
    private async Task CaptureFlightAsync(int roundOrdinal, CompetitorId competitorRef, bool fullMetrics)
    {
        var entryId = await ApiClient.PostCommandAsync<EntryId>(
            Client, "/open-entry", new OpenEntry(_competitionId, 0, roundOrdinal, 1, _groupRef, competitorRef));

        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(entryId));

        await CaptureAsync(entryId, "flightTime", MeasuredValue.Of(300m));
        if (!fullMetrics)
        {
            return;
        }

        await CaptureAsync(entryId, "startHeight", MeasuredValue.Of(0m));
        await CaptureAsync(entryId, "startHeightRecorded", MeasuredValue.Of(true));
        await CaptureAsync(entryId, "landingDistance", MeasuredValue.Of(100m));
        await CaptureAsync(entryId, "overflySeconds", MeasuredValue.Of(0m));
        await CaptureAsync(entryId, "touchedByCompetitor", MeasuredValue.Of(false));
    }

    private static async Task CaptureAsync(EntryId entryId, string metric, MeasuredValue value) =>
        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/capture-measurement", new CaptureMeasurement(entryId, 1, metric, value));
}
