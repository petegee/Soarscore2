// kanban/in-progress/prescribed-draw-import.md WI-6 — step definitions for
// Features/PrescribingADraw.feature: the prescribed-draw path end to end over
// real HTTP (prescribe → read back → reject → re-prescribe → accept), the
// competitorMissing defect surfaced as a problem detail, and the TaskRef-
// carrying payload shape a catalogue phase requires. Mirrors
// AcceptingTheDrawSteps.cs file-by-file; step texts are deliberately worded
// apart from every other binding class — Reqnroll bindings are global, and a
// step regex shared verbatim across two Binding classes is ambiguous (that
// file's own header note). The prescribe steps take optional round/task
// columns so one table shape covers FixedSequence and catalogue phases alike.

using System.Net;
using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.SeedData;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class PrescribingADrawSteps
{
    private static HttpClient Client => AcceptanceFixture.Client;

    // The trust model has no auth — By is a self-declared CD name, required
    // non-empty by the handler because an absent PrescribedBy would make the
    // log unable to tell a prescribed draw from a generated one.
    private const string CdName = "Test CD";

    private string? _classContentHash;
    private CompetitionId _competitionId;
    private readonly List<CompetitorId> _competitors = [];
    private List<string> _namedTaskRefs = [];
    private HttpResponseMessage? _rawResponse;

    // ---------------------------------------------------------------- Given

    [Given(@"^a published (.+) rulebook for prescribed drawing$")]
    public async Task GivenAPublishedRulebookForPrescribedDrawing(string faiDesignation)
    {
        var fileName = faiDesignation switch
        {
            "F3K" => "10-f3k",
            "F5J" => "30-f5j",
            _ => throw new ArgumentException($"No corpus file mapped for {faiDesignation}."),
        };
        var definition = Corpus.All.Single(c => c.FileName == fileName).Definition;
        _classContentHash = await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(definition));
    }

    [Given(@"^a prescribed-draw competition with (\d+) registered competitors$")]
    public async Task GivenAPrescribedDrawCompetitionWithRegisteredCompetitors(int count)
    {
        var slug = Guid.NewGuid().ToString("N");
        _competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/create-competition",
            new CreateCompetition(
                $"Prescribed Draw {slug}", "Taupo",
                new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), _classContentHash!));

        for (var i = 0; i < count; i++)
        {
            var email = $"pilot-prescribedraw-{slug}-{i}@example.com".ToLowerInvariant();
            var personId = await ApiClient.PostCommandAsync<PersonId>(
                Client, "/register-person", new RegisterPerson($"Pilot {i + 1}", new ContactDetails { Email = email }, null));
            var competitorId = await ApiClient.PostCommandAsync<CompetitorId>(
                Client, "/register-competitor", new RegisterCompetitor(_competitionId, personId));
            _competitors.Add(competitorId);
        }
    }

    // ----------------------------------------------------------------- When

    [When(@"^the contest director prescribes the preliminary phase setting these groups$")]
    public async Task WhenTheContestDirectorPrescribesThePreliminaryPhase(Table table) =>
        await PrescribeAsync(table);

    [When(@"^the contest director prescribes the corrected schedule$")]
    public async Task WhenTheContestDirectorPrescribesTheCorrectedSchedule(Table table) =>
        await PrescribeAsync(table);

    [When(@"^the contest director prescribes the preliminary phase naming these tasks and groups$")]
    public async Task WhenTheContestDirectorPrescribesNamingTasksAndGroups(Table table)
    {
        _namedTaskRefs = table.Rows.Select(row => row["task"]).ToList();
        await PrescribeAsync(table);
    }

    [When(@"^the contest director tries to prescribe the preliminary phase setting these groups$")]
    public async Task WhenTheContestDirectorTriesToPrescribeThePreliminaryPhase(Table table) =>
        _rawResponse = await ApiClient.PostCommandRawAsync(
            Client, "/prescribe-draw", new PrescribeDraw(_competitionId, BuildRounds(table), CdName));

    [When(@"^the contest director rejects the prescribed draw because ""(.+)""$")]
    public async Task WhenTheContestDirectorRejectsThePrescribedDrawBecause(string reason)
    {
        _rawResponse = await ApiClient.PostCommandRawAsync(
            Client, "/reject-draw", new RejectDraw(_competitionId, reason));
        _rawResponse.EnsureSuccessStatusCode();
    }

    [When(@"^the contest director accepts the prescribed draw$")]
    public async Task WhenTheContestDirectorAcceptsThePrescribedDraw() =>
        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/accept-draw", new AcceptDraw(_competitionId));

    // ----------------------------------------------------------------- Then

    // SeqNo preservation is the story's decision 4: members are stored in the
    // flying order sent, so the read surface must show exactly that sequence —
    // not sorted, not regrouped. Ordinals are assigned by position (never
    // supplied), which is also what this asserts.
    [Then(@"^the competition reads these groups in these flying orders$")]
    public async Task ThenTheCompetitionReadsTheseGroupsInTheseFlyingOrders(Table table)
    {
        var expected = table.Rows
            .Select(row => (Ordinal: int.Parse(row["group"]), Members: FlyingOrderOrdinals(row["flying order"])))
            .OrderBy(g => g.Ordinal)
            .ToList();

        var phase = await CompetitionPhaseAsync();
        var groups = phase.Rounds.Single().TaskRounds.Single().Groups
            .OrderBy(g => g.Ordinal).ToList();

        groups.Select(g => g.Ordinal).Should().Equal(expected.Select(g => g.Ordinal));

        for (var i = 0; i < expected.Count; i++)
        {
            groups[i].CompetitorRefs
                .Select(c => _competitors.IndexOf(c) + 1)
                .Should().Equal(expected[i].Members);
        }
    }

    [Then(@"^the prescription is refused because pilot (\d+) is left unplaced$")]
    public async Task ThenThePrescriptionIsRefusedBecausePilotIsLeftUnplaced(int unplacedOrdinal)
    {
        // The domain's competitorMissing message does not name the pilot (it
        // says an eligible competitor appears in no group), so like
        // DrawingACatalogueChoiceSteps this asserts the stable failure code;
        // the ordinal stays in the step text where a reader can see it.
        _rawResponse!.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadProblemTitleAsync(_rawResponse)).Should().Be("prescribeDraw.competitorMissing");
    }

    [Then(@"^the competition reads as having nothing drawn$")]
    public async Task ThenTheCompetitionReadsAsHavingNothingDrawn()
    {
        // The refusal appended nothing: no PhaseDrawn means no live phase at
        // all (D2 removal semantics keep Phases holding live phases only).
        var view = await CompetitionViewAsync();
        view.Competition.Phases.Should().BeEmpty();
    }

    [Then(@"^each prescribed round carries its named task with the field placed exactly once$")]
    public async Task ThenEachPrescribedRoundCarriesItsNamedTask()
    {
        var phase = await CompetitionPhaseAsync();
        var rounds = phase.Rounds.OrderBy(r => r.Ordinal).ToList();

        rounds.Select(r => r.TaskRounds.Single().TaskRef).Should().Equal(_namedTaskRefs);

        foreach (var round in rounds)
        {
            var placed = round.TaskRounds.Single().Groups.Single().CompetitorRefs;
            placed.Should().BeEquivalentTo(_competitors);
            placed.Distinct().Count().Should().Be(_competitors.Count);
        }
    }

    [Then(@"^the competition reads as having its prescribed draw accepted$")]
    public async Task ThenTheCompetitionReadsAsHavingItsPrescribedDrawAccepted()
    {
        // Folded state: the live phase's Draw.Status.
        var view = await CompetitionViewAsync();
        view.Competition.Phases.Single().Draw.Status.Should().Be("accepted");

        // Read model: the summary's State moved with acceptance, exactly as it
        // does on the generated path (AcceptingTheDrawSteps' D8 check).
        var summaries = await ApiClient.GetAsync<IReadOnlyList<CompetitionSummary>>(
            Client, $"/competitions?classContentHash={_classContentHash}");
        summaries.Single(s => s.Id == _competitionId).State.Should().Be("accepted");
    }

    // ------------------------------------------------------------ Helpers

    private async Task PrescribeAsync(Table table) =>
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/prescribe-draw", new PrescribeDraw(_competitionId, BuildRounds(table), CdName));

    // One row per group; the round, task and group columns are all optional so
    // a FixedSequence round needs none of task/group (a single unnamed group,
    // TaskRef null) while a catalogue phase names its task per round. Rows
    // carry no ordinals into the payload — group and round ordinals are
    // assigned by position.
    private IReadOnlyList<PrescribedRound> BuildRounds(Table table)
    {
        var hasRound = table.Header.Any(header => header == "round");
        var hasTask = table.Header.Any(header => header == "task");
        var hasGroup = table.Header.Any(header => header == "group");

        return table.Rows
            .Select((row, index) => (
                Round: hasRound ? int.Parse(row["round"]) : 1,
                TaskRef: hasTask ? row["task"] : null,
                Group: hasGroup ? int.Parse(row["group"]) : index + 1,
                Members: ParseFlyingOrder(row["flying order"])))
            .GroupBy(r => r.Round)
            .OrderBy(g => g.Key)
            .Select(roundRows => new PrescribedRound(
                TaskRef: roundRows.First().TaskRef,
                Groups: [.. roundRows.OrderBy(r => r.Group).Select(r => new PrescribedGroup(r.Members))]))
            .ToList();
    }

    private List<CompetitorId> ParseFlyingOrder(string csv) =>
        FlyingOrderOrdinals(csv).Select(ordinal => _competitors[ordinal - 1]).ToList();

    // Competitors appear in the feature tables by their registration ordinal
    // ("pilot 6"), the same convention every other feature uses; the comma
    // list IS the flying order sent.
    private static IEnumerable<int> FlyingOrderOrdinals(string csv) =>
        csv.Split(',').Select(s => int.Parse(s.Trim()));

    private async Task<Phase> CompetitionPhaseAsync()
    {
        var view = await CompetitionViewAsync();
        return view.Competition.Phases.Single();
    }

    private async Task<CompetitionView> CompetitionViewAsync() =>
        await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");

    private static async Task<string> ReadProblemTitleAsync(HttpResponseMessage response)
    {
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("title").GetString()!;
    }
}
