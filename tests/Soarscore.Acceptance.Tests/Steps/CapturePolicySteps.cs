// authentication-and-authorisation.md WI-10 — the shared steps of
// Features/CapturePolicy.feature and Features/Integrations.feature: every
// acting principal is a persona or machine token over real HTTP against the
// mock-mode factory, and every competition is per-scenario (the suite's
// unique-name discipline), built by an organiser token.
//
// Step phrasings are deliberately distinct from CapturingAScoreSteps' — those
// post to the none-mode factory, and a duplicate regex would be an ambiguous
// binding. Integrations.feature reuses these Givens verbatim through
// context injection (the DrawAcceptanceState pattern), which is why its own
// steps class defines no competition of its own.

using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class CapturePolicySteps
{
    private readonly CapturePolicyState _state;
    

    public CapturePolicySteps(CapturePolicyState state) => _state = state;

    private static string Pete => TestJwt.ForPerson(AuthActors.Organiser);

    private async Task<PersonId> PersonIdOfAsync(AuthPersona persona)
    {
        if (_state.PersonIds.TryGetValue(persona.Slug, out var known))
        {
            return known;
        }

        var response = await AuthApi.PostAsync(TestJwt.ForPerson(persona), "/link-sign-in", new LinkSignIn());
        response.EnsureSuccessStatusCode();
        var personId = (await AuthApi.ReadAsync<LinkSignInView>(response)).PersonId;
        _state.PersonIds[persona.Slug] = personId;
        return personId;
    }

    // ---------------------------------------------------------------- Given

    [Given(@"^Pete has signed in$")]
    public void GivenPeteHasSignedIn()
    {
        // Nothing to record: the organiser's authority rides the token (D2),
        // and Pete's PersonId is only ever a target, never a guess — the
        // bind-identity step resolves the person it creates itself.
    }

    [Given(@"^an F5J competition Pete created with (\d+) registered competitors$")]
    public async Task GivenAnF5JCompetitionPeteCreatedWithRegisteredCompetitors(int count)
    {
        var contentHash = await AuthApi.PostCommandAsync<string>(
            Pete, "/publish-class-definition",
            new PublishClassDefinition(Corpus.All.Single(c => c.FileName == "30-f5j").Definition));

        var slug = Guid.NewGuid().ToString("N");
        _state.CompetitionId = await AuthApi.PostCommandAsync<CompetitionId>(
            Pete, "/create-competition",
            new CreateCompetition($"Acceptance {slug}", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), contentHash));

        var competitors = new List<CompetitorId>();
        for (var i = 0; i < count; i++)
        {
            var personId = await AuthApi.PostCommandAsync<PersonId>(
                Pete, "/register-person",
                new RegisterPerson($"Pilot {slug}-{i}", new ContactDetails { Email = $"pilot-{slug}-{i}@example.com".ToLowerInvariant() }, null));
            competitors.Add(await AuthApi.PostCommandAsync<CompetitorId>(
                Pete, "/register-competitor", new RegisterCompetitor(_state.CompetitionId, personId)));
        }

        _state.Competitors = competitors;
    }

    [Given(@"^the organiser drew a preliminary phase of (\d+) rounds and accepted it$")]
    public async Task GivenTheOrganiserDrewAPreliminaryPhaseAndAcceptedIt(int rounds)
    {
        (await AuthApi.PostAsync(Pete, "/draw-phase", new DrawPhase(_state.CompetitionId, rounds))).EnsureSuccessStatusCode();
        (await AuthApi.PostAsync(Pete, "/accept-draw", new AcceptDraw(_state.CompetitionId))).EnsureSuccessStatusCode();
    }

    [Given(@"^an entry opened for competitor (\d+) in round (\d+), group (\d+) with its first flight open$")]
    public async Task GivenAnEntryOpenedForCompetitorInRoundGroupWithFirstFlightOpen(
        int competitorOrdinal, int roundOrdinal, int groupOrdinal)
    {
        var groupId = await AuthApi.ResolveGroupIdAsync(Pete, _state.CompetitionId.Value, roundOrdinal, groupOrdinal);
        _state.EntryId = await AuthApi.PostCommandAsync<EntryId>(Pete, "/open-entry",
            new OpenEntry(_state.CompetitionId, 0, roundOrdinal, 1, groupId, _state.Competitors[competitorOrdinal - 1]));
        (await AuthApi.PostAsync(Pete, "/open-flight", new OpenFlight(_state.EntryId))).EnsureSuccessStatusCode();
    }

    // ----------------------------------------------------------------- When

    [When(@"^Pete allow-lists (Tama|the machine person) for capture$")]
    public async Task WhenPeteAllowListsForCapture(string who)
    {
        var personId = who == "Tama"
            ? await PersonIdOfAsync(AuthActors.Competitor)
            : _state.MachinePersonId ?? throw new InvalidOperationException("the machine person is not bound yet");
        _state.LastResponse = await AuthApi.PostAsync(Pete, "/configure-capture-policy",
            new ConfigureCapturePolicy(_state.CompetitionId, new CapturePolicy(CapturePolicyMode.AllowList, [personId])));
        _state.LastResponse.EnsureSuccessStatusCode();
    }

    [When(@"^(Tama|Pete|FieldRig) captures flightTime of (\d+) seconds on the entry$")]
    public async Task WhenThePersonaCapturesFlightTimeOnTheEntry(string who, int seconds)
    {
        var token = who switch
        {
            "Tama" => TestJwt.ForPerson(AuthActors.Competitor),
            "FieldRig" => TestJwt.ForPerson(AuthActors.Unlinked),
            _ => Pete,
        };
        _state.LastResponse = await AuthApi.PostAsync(token, "/capture-measurement",
            new CaptureMeasurement(_state.EntryId, 1, "flightTime", MeasuredValue.Of((decimal)seconds)));
    }

    [When(@"^Tama opens flight (\d+) on the entry$")]
    public async Task WhenTamaOpensFlightOnTheEntry(int sequence)
    {
        _state.LastResponse = await AuthApi.PostAsync(
            TestJwt.ForPerson(AuthActors.Competitor), "/open-flight", new OpenFlight(_state.EntryId, sequence));
        _state.LastResponse.EnsureSuccessStatusCode();
    }

    [When(@"^Tama captures flightTime of (\d+) seconds on flight (\d+) of the entry$")]
    public async Task WhenTamaCapturesFlightTimeOnFlightOfTheEntry(int seconds, int sequence)
    {
        _state.LastResponse = await AuthApi.PostAsync(
            TestJwt.ForPerson(AuthActors.Competitor), "/capture-measurement",
            new CaptureMeasurement(_state.EntryId, sequence, "flightTime", MeasuredValue.Of((decimal)seconds)));
    }

    // ----------------------------------------------------------------- Then

    [Then(@"^the capture is accepted$")]
    public void ThenTheCaptureIsAccepted() =>
        _state.LastResponse!.IsSuccessStatusCode.Should().BeTrue(
            "the capture should be accepted, not {0}", _state.LastResponse.StatusCode);

    [Then(@"^the response is 403 refusing with auth\.capturePolicy\.denied$")]
    public async Task ThenTheResponseIs403RefusingWithCapturePolicyDenied()
    {
        _state.LastResponse!.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AuthApi.ProblemTitleAsync(_state.LastResponse)).Should().Be("auth.capturePolicy.denied");
    }

    [Then(@"^the entry holds two flights with flightTimes (\d+) and (\d+)$")]
    public async Task ThenTheEntryHoldsTwoFlightsWithFlightTimes(int firstSequence, int secondSequence)
    {
        var entry = await EntryReader.LoadAsync(AuthAcceptanceFixture.EventStore, _state.EntryId, TestContext.Current.CancellationToken);

        entry.Flights.Should().HaveCount(2);
        entry.Flights[0].Measurements.Should().ContainSingle(m => m.Metric == "flightTime")
            .Which.Value.Number.Should().Be((decimal)firstSequence);
        entry.Flights[1].Measurements.Should().ContainSingle(m => m.Metric == "flightTime")
            .Which.Value.Number.Should().Be((decimal)secondSequence);
    }

    internal sealed record LinkSignInView(PersonId PersonId);
}
