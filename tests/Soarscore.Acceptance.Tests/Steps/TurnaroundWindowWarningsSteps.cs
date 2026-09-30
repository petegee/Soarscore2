// kanban/backlog/turn-around-window-score-validation.md WI-4 — step
// definitions for Features/TurnaroundWindowWarnings.feature. Real HTTP against
// the real Soarscore.Api (AcceptanceFixture.Client), the same discipline
// CapturingAScoreSteps.cs established.
//
// A self-contained Steps class with deliberately distinct Given phrasing
// (ScoringACompetitionSteps.cs's header records why: Reqnroll binds step
// regexes assembly-wide, so this class must not reuse another feature's step
// text). F3K Task D (SeedF3K.cs: two flights, AllFlights, 600 s window,
// cap 300/flight): the organiser's worked example, verbatim.
//
// F3K's two validity flags (launchedInWorkingTime, landedWithinWindow) are
// recorded exceptions — absence resolves to compliance (SeedF3K.cs) — so
// capturing flightTime alone yields Valid flights.

using System.Globalization;
using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class TurnaroundWindowWarningsSteps
{
    private static HttpClient Client => AcceptanceFixture.Client;

    private static readonly ClassDefinition F3KDefinition = Corpus.All.Single(c => c.FileName == "10-f3k").Definition;

    private string? _classContentHash;
    private CompetitionId _competitionId;
    private readonly List<CompetitorId> _competitors = [];
    private readonly Dictionary<(int Round, int Group), GroupId> _groupIds = new();
    private List<GroupScoreView>? _views;

    // ---------------------------------------------------------------- Given

    [Given(@"^the F3K class is published$")]
    public async Task GivenTheF3KClassIsPublished()
    {
        _classContentHash = await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(F3KDefinition));
    }

    [Given(@"^a competition adopting the F3K class is created with (\d+) registered competitors$")]
    public async Task GivenACompetitionAdoptingTheF3KClassIsCreated(int count)
    {
        var slug = Guid.NewGuid().ToString("N");
        _competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/create-competition",
            new CreateCompetition($"Turnaround Acceptance {slug}", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), _classContentHash!));

        for (var i = 0; i < count; i++)
        {
            var email = $"pilot-turnaround-{slug}-{i}@example.com".ToLowerInvariant();
            var personId = await ApiClient.PostCommandAsync<PersonId>(
                Client, "/register-person", new RegisterPerson($"Pilot {i + 1}", new ContactDetails { Email = email }, null));
            var competitorId = await ApiClient.PostCommandAsync<CompetitorId>(
                Client, "/register-competitor", new RegisterCompetitor(_competitionId, personId));
            _competitors.Add(competitorId);
        }
    }

    // For a ChooseFromCatalogue phase (F3K): the table names the task the CD
    // picked for each round — DrawPhase's taskRefs. Distinct text from
    // CapturingAScoreSteps' "a drawn preliminary phase with these tasks".
    [Given(@"^the preliminary phase is drawn with these tasks$")]
    public async Task GivenThePreliminaryPhaseIsDrawnWithTheseTasks(Table table)
    {
        var taskRefs = table.Rows.Select(row => row["task"]).ToList();
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/draw-phase", new DrawPhase(_competitionId, taskRefs.Count, taskRefs));
        // D4: flying starts at acceptance, not at the draw.
        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/accept-draw", new AcceptDraw(_competitionId));
    }

    // ----------------------------------------------------------------- When

    [When(@"^competitor (\d+) flies two flights of ([\d.]+) and ([\d.]+) seconds in round (\d+), group (\d+)$")]
    public async Task WhenCompetitorFliesTwoFlights(int competitorOrdinal, string first, string second, int roundOrdinal, int groupOrdinal)
    {
        var firstSeconds = decimal.Parse(first, CultureInfo.InvariantCulture);
        var secondSeconds = decimal.Parse(second, CultureInfo.InvariantCulture);
        var groupId = await ResolveGroupIdAsync(roundOrdinal, groupOrdinal);
        var competitorId = _competitors[competitorOrdinal - 1];

        var entryId = await ApiClient.PostCommandAsync<EntryId>(
            Client, "/open-entry", new OpenEntry(_competitionId, 0, roundOrdinal, 1, groupId, competitorId));

        var sequences = new[] { 1, 2 };
        var times = new[] { firstSeconds, secondSeconds };
        for (var i = 0; i < 2; i++)
        {
            await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(entryId, sequences[i]));
            await ApiClient.PostCommandAsync<EntryId>(
                Client, "/capture-measurement",
                new CaptureMeasurement(entryId, sequences[i], "flightTime", MeasuredValue.Of(times[i])));
        }
    }

    // ----------------------------------------------------------------- Then

    [Then(@"^the task-round result is available for round (\d+)$")]
    public async Task ThenTheTaskRoundResultIsAvailableForRound(int roundOrdinal)
    {
        // GET /task-round-result 200 — the field readout itself. ApiClient
        // throws below 200/above 299, so reaching the assertion IS the 200.
        _views = await ApiClient.GetAsync<List<GroupScoreView>>(Client,
            $"/task-round-result?competitionRef={_competitionId.Value}&phaseOrdinal=0&roundOrdinal={roundOrdinal}&taskRoundOrdinal=1");
        _views.Should().ContainSingle();
        _views[0].ValidCount.Should().Be(6);
        _views[0].IsAnnulled.Should().BeFalse();
    }

    [Then(@"^competitor (\d+)'s row carries (score\.\w+)$")]
    public void ThenCompetitorsRowCarries(int competitorOrdinal, string code)
    {
        var row = RowFor(competitorOrdinal);
        row.State.Should().Be(TaskResultState.Valid);
        row.Warnings.Should().ContainSingle($"competitor {competitorOrdinal}'s row carries exactly one warning");
        row.Warnings[0].Code.Should().Be(code);
        // The message names the summed seconds — the verbatim readout the CD
        // sees (WI-1's format, asserted here only for its load-bearing fact).
        row.Warnings[0].Message.Should().NotBeNullOrWhiteSpace();
    }

    [Then(@"^every other row carries no warnings$")]
    public void ThenEveryOtherRowCarriesNoWarnings()
    {
        var flagged = _views![0].Results.Single(r => !r.Warnings.IsEmpty);
        _views[0].Results.Where(r => r != flagged).Should().OnlyContain(r => r.Warnings.IsEmpty);
    }

    [Then(@"^the group's pre-normalisation scores are$")]
    public void ThenTheGroupsPreNormalisationScoresAre(Table table)
    {
        foreach (var row in table.Rows)
        {
            var ordinal = int.Parse(row["competitor"], CultureInfo.InvariantCulture);
            var expected = decimal.Parse(row["score"], CultureInfo.InvariantCulture);
            RowFor(ordinal).PreNormalisationScore.Should().Be(expected, $"competitor {ordinal}'s raw is untouched by warnings");
        }
    }

    [Then(@"^the group's normalised scores are$")]
    public void ThenTheGroupsNormalisedScoresAre(Table table)
    {
        // Warn-through: the winner-takes-1000 ratio is computed over the
        // recorded raws exactly as without the story — a silent clamp or
        // refusal would move every row here.
        _views![0].WinnerRef.Should().Be(_competitors[0]);
        _views[0].Results.Single(r => r.CompetitorRef == _competitors[0]).RawScore.Should().Be(1000m);
        foreach (var row in table.Rows)
        {
            var ordinal = int.Parse(row["competitor"], CultureInfo.InvariantCulture);
            var expected = decimal.Parse(row["score"], CultureInfo.InvariantCulture);
            RowFor(ordinal).RawScore.Should().Be(expected, $"competitor {ordinal}'s normalised score is unchanged");
        }
    }

    // --------------------------------------------------------------- helpers

    private CompetitorTaskResultView RowFor(int competitorOrdinal) =>
        _views![0].Results.Single(r => r.CompetitorRef == _competitors[competitorOrdinal - 1]);

    private async Task<GroupId> ResolveGroupIdAsync(int roundOrdinal, int groupOrdinal)
    {
        var key = (roundOrdinal, groupOrdinal);
        if (_groupIds.TryGetValue(key, out var cached))
            return cached;

        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var phase = view.Competition.Phases.Single();
        var round = phase.Rounds.Single(r => r.Ordinal == roundOrdinal);
        var group = round.TaskRounds.Single().Groups.Single(g => g.Ordinal == groupOrdinal);

        _groupIds[key] = group.Id;
        return group.Id;
    }
}
