// kanban/in-progress/draw-acceptance-redraw.md WI-7 — step definitions for
// Features/AcceptingTheDraw.feature: the glossary's draw sentence end to end
// (draw → accept / reject → redraw → accept), against the real Api over the
// real store. Step texts are deliberately worded apart from their
// CapturingAScoreSteps cousins — Reqnroll bindings are global, and this
// feature needs its own scenario-scoped state (a competition that stays
// deliberately unaccepted in two scenarios, which no other feature does).
//
// lane-assignment.md WI-8 composes this feature's shared draw Givens verbatim
// into Features/AssigningSpots.feature, so the competition and competitor list
// they create are also published into DrawAcceptanceState (context injection —
// see that file's header): the one scenario-scoped bridge that makes the
// composition work across two Binding classes. The write below is the only
// change to this class; its own scenarios neither read nor need it.

using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class AcceptingTheDrawSteps
{
    private static HttpClient Client => AcceptanceFixture.Client;

    private string? _classContentHash;
    private CompetitionId _competitionId;
    private readonly List<CompetitorId> _competitors = [];
    private EntryId _entryId;
    private HttpResponseMessage? _rawResponse;
    private readonly DrawAcceptanceState _state;

    public AcceptingTheDrawSteps(DrawAcceptanceState state) => _state = state;

    // ---------------------------------------------------------------- Given

    [Given(@"^a published F5J rulebook for draw acceptance$")]
    public async Task GivenAPublishedF5JRulebookForDrawAcceptance()
    {
        var definition = ResolveF5J();
        _classContentHash = await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(definition));
    }

    [Given(@"^a draw-acceptance competition with (\d+) registered competitors$")]
    public async Task GivenADrawAcceptanceCompetitionWithRegisteredCompetitors(int count)
    {
        var slug = Guid.NewGuid().ToString("N");
        _competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/create-competition",
            new CreateCompetition(
                $"Draw Acceptance {slug}", "Taupo",
                new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), _classContentHash!));

        for (var i = 0; i < count; i++)
        {
            var email = $"pilot-drawaccept-{slug}-{i}@example.com".ToLowerInvariant();
            var personId = await ApiClient.PostCommandAsync<PersonId>(
                Client, "/register-person", new RegisterPerson($"Pilot {i + 1}", new ContactDetails { Email = email }, null));
            var competitorId = await ApiClient.PostCommandAsync<CompetitorId>(
                Client, "/register-competitor", new RegisterCompetitor(_competitionId, personId));
            _competitors.Add(competitorId);
        }

        // lane-assignment.md WI-8: the shared Givens publish their scenario
        // state for the Binding classes composing them (see class header).
        _state.CompetitionId = _competitionId;
        _state.Competitors = _competitors;
    }

    [Given(@"^its preliminary phase has been drawn for review$")]
    public async Task GivenItsPreliminaryPhaseHasBeenDrawnForReview() =>
        await DrawAsync(rounds: 1);

    [Given(@"^its preliminary phase has been drawn but not accepted$")]
    public async Task GivenItsPreliminaryPhaseHasBeenDrawnButNotAccepted() =>
        await DrawAsync(rounds: 1);

    [Given(@"^its preliminary phase has been drawn and accepted$")]
    public async Task GivenItsPreliminaryPhaseHasBeenDrawnAndAccepted()
    {
        await DrawAsync(rounds: 1);
        await AcceptAsync();
    }

    [Given(@"^an entry has been opened and a flight recorded for competitor (\d+) in round (\d+), group (\d+)$")]
    public async Task GivenAnEntryHasBeenOpenedAndAFlightRecorded(int competitorOrdinal, int roundOrdinal, int groupOrdinal)
    {
        await OpenEntryAsync(competitorOrdinal, roundOrdinal, groupOrdinal);
        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(_entryId, 1));
    }

    // ----------------------------------------------------------------- When

    [When(@"^the contest director accepts the draw$")]
    public async Task WhenTheContestDirectorAcceptsTheDraw() =>
        await AcceptAsync();

    [When(@"^the contest director rejects the draw because ""(.+)""$")]
    public async Task WhenTheContestDirectorRejectsTheDrawBecause(string reason)
    {
        _rawResponse = await ApiClient.PostCommandRawAsync(
            Client, "/reject-draw", new RejectDraw(_competitionId, reason));
        _rawResponse.EnsureSuccessStatusCode();
    }

    [When(@"^the contest director tries to reject the draw because ""(.+)""$")]
    public async Task WhenTheContestDirectorTriesToRejectTheDrawBecause(string reason) =>
        _rawResponse = await ApiClient.PostCommandRawAsync(
            Client, "/reject-draw", new RejectDraw(_competitionId, reason));

    [When(@"^a latecomer registers while no draw stands$")]
    public async Task WhenALatecomerRegistersWhileNoDrawStands()
    {
        // The field freeze lifted with the rejection (D6): registration must
        // succeed where it would have been refused under the old
        // frozen-at-draw rule. A failure here is the scenario failing.
        var slug = Guid.NewGuid().ToString("N");
        var personId = await ApiClient.PostCommandAsync<PersonId>(
            Client, "/register-person",
            new RegisterPerson("Late Arrival", new ContactDetails { Email = $"late-{slug}@example.com" }, null));
        var competitorId = await ApiClient.PostCommandAsync<CompetitorId>(
            Client, "/register-competitor", new RegisterCompetitor(_competitionId, personId));
        _competitors.Add(competitorId);
    }

    [When(@"^the contest director redraws the preliminary phase$")]
    public async Task WhenTheContestDirectorRedrawsThePreliminaryPhase() =>
        await DrawAsync(rounds: 1);

    [When(@"^the scorer tries to open an entry for competitor (\d+) in round (\d+), group (\d+)$")]
    public async Task WhenTheScorerTriesToOpenAnEntry(int competitorOrdinal, int roundOrdinal, int groupOrdinal) =>
        _rawResponse = await PostOpenEntryRawAsync(competitorOrdinal, roundOrdinal, groupOrdinal);

    [When(@"^the scorer now opens an entry for competitor (\d+) in round (\d+), group (\d+)$")]
    public async Task WhenTheScorerNowOpensAnEntry(int competitorOrdinal, int roundOrdinal, int groupOrdinal) =>
        await OpenEntryAsync(competitorOrdinal, roundOrdinal, groupOrdinal);

    [When(@"^competitor (\d+) withdraws from the competition$")]
    public async Task WhenCompetitorWithdrawsFromTheCompetition(int competitorOrdinal)
    {
        // Withdrawal stays ungated forever — accepted draw or not (D6).
        await ApiClient.PostCommandAsync<CompetitorId>(
            Client, "/withdraw-competitor",
            new WithdrawCompetitor(_competitionId, _competitors[competitorOrdinal - 1]));
    }

    // ----------------------------------------------------------------- Then

    [Then(@"^the competition reads as having an accepted draw$")]
    public async Task ThenTheCompetitionReadsAsHavingAnAcceptedDraw()
    {
        // Folded state: the live phase's Draw.Status.
        var view = await CompetitionViewAsync();
        view.Competition.Phases.Single().Draw.Status.Should().Be("accepted");

        // Read model: the summary's State moved with acceptance (D8).
        var summaries = await ApiClient.GetAsync<IReadOnlyList<CompetitionSummary>>(
            Client, $"/competitions?classContentHash={_classContentHash}");
        summaries.Single(s => s.Id == _competitionId).State.Should().Be("accepted");
    }

    [Then(@"^the redrawn field holds (\d+) competitors$")]
    public async Task ThenTheRedrawnFieldHoldsCompetitors(int expected)
    {
        // D2 ordinal-correctness, end to end: the replacement draw addresses
        // phase definition 0 again (not the flyoff), and its groups are drawn
        // from the field as it now stands — latecomer included, everyone once.
        var view = await CompetitionViewAsync();
        var placed = view.Competition.Phases.Single().Rounds
            .SelectMany(r => r.TaskRounds[0].Groups)
            .SelectMany(g => g.CompetitorRefs)
            .ToArray();

        placed.Length.Should().Be(expected);
        placed.Distinct().Count().Should().Be(expected);
        placed.Should().BeSubsetOf(_competitors);
    }

    [Then(@"^the rejection is refused because entries exist against the phase$")]
    public async Task ThenTheRejectionIsRefusedBecauseEntriesExistAgainstThePhase()
    {
        _rawResponse!.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await ReadProblemTitleAsync(_rawResponse)).Should().Be("rejectDraw.entriesExist");
    }

    [Then(@"^the open is refused because the draw is not yet accepted$")]
    public async Task ThenTheOpenIsRefusedBecauseTheDrawIsNotYetAccepted()
    {
        _rawResponse!.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await ReadProblemTitleAsync(_rawResponse)).Should().Be("entry.drawNotAccepted");
    }

    [Then(@"^that entry appears in the index for round (\d+), group (\d+)$")]
    public async Task ThenThatEntryAppearsInTheIndexForRoundGroup(int roundOrdinal, int groupOrdinal)
    {
        var groupId = await ResolveGroupIdAsync(roundOrdinal, groupOrdinal);
        var url = $"/entries?competitionRef={_competitionId.Value}&phaseOrdinal=0&roundOrdinal={roundOrdinal}&taskRoundOrdinal=1&groupRef={groupId.Value}";

        var matches = await ApiClient.GetAsync<IReadOnlyList<EntrySummary>>(Client, url);

        matches.Should().ContainSingle(e => e.Id == _entryId);
    }

    [Then(@"^the competition still reads as having an accepted draw$")]
    public async Task ThenTheCompetitionStillReadsAsHavingAnAcceptedDraw()
    {
        // Withdrawal honours but does not disturb the draw (D6): the folded
        // status survives AND the read model did not move off "accepted".
        await ThenTheCompetitionReadsAsHavingAnAcceptedDraw();
    }

    // ------------------------------------------------------------ Helpers

    private async Task DrawAsync(int rounds) =>
        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/draw-phase", new DrawPhase(_competitionId, rounds));

    private async Task AcceptAsync() =>
        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/accept-draw", new AcceptDraw(_competitionId));

    private async Task OpenEntryAsync(int competitorOrdinal, int roundOrdinal, int groupOrdinal)
    {
        _entryId = await ApiClient.PostCommandAsync<EntryId>(
            Client, "/open-entry", await BuildOpenEntryAsync(competitorOrdinal, roundOrdinal, groupOrdinal));
    }

    private async Task<HttpResponseMessage> PostOpenEntryRawAsync(int competitorOrdinal, int roundOrdinal, int groupOrdinal) =>
        await ApiClient.PostCommandRawAsync(
            Client, "/open-entry", await BuildOpenEntryAsync(competitorOrdinal, roundOrdinal, groupOrdinal));

    private async Task<OpenEntry> BuildOpenEntryAsync(int competitorOrdinal, int roundOrdinal, int groupOrdinal) =>
        new(_competitionId, 0, roundOrdinal, 1, await ResolveGroupIdAsync(roundOrdinal, groupOrdinal),
            _competitors[competitorOrdinal - 1]);

    private async Task<GroupId> ResolveGroupIdAsync(int roundOrdinal, int groupOrdinal)
    {
        var view = await CompetitionViewAsync();
        var round = view.Competition.Phases.Single().Rounds.Single(r => r.Ordinal == roundOrdinal);
        return round.TaskRounds.Single().Groups.Single(g => g.Ordinal == groupOrdinal).Id;
    }

    private async Task<CompetitionView> CompetitionViewAsync() =>
        await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");

    private static async Task<string> ReadProblemTitleAsync(HttpResponseMessage response)
    {
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("title").GetString()!;
    }

    private static ClassDefinition ResolveF5J() =>
        Corpus.All.Single(c => c.FileName == "30-f5j").Definition;
}
