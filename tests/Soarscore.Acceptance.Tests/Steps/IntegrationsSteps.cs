// authentication-and-authorisation.md WI-10 — Integrations.feature's steps
// (D12): the machine actor's sub carries no |, so HttpCurrentUser resolves
// provider client-credentials, subject the client id. The machine is nobody
// (an unlinked identity) until an organiser binds it with /bind-identity and
// the competition's allow-list names it — and binding alone, on a competition
// with the default policy, still refuses: authority is configuration, not the
// identity's existence. The competition/entry Givens are CapturePolicySteps',
// shared through CapturePolicyState context injection.

using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class IntegrationsSteps
{
    private readonly CapturePolicyState _state;
    private string _machineClientId = AuthActors.MachineClientId;

    public IntegrationsSteps(CapturePolicyState state) => _state = state;

    // ----------------------------------------------------------------- When

    [When(@"^the field-clock-rig client reads /who-am-i$")]
    public async Task WhenTheMachineClientReadsWhoAmI()
    {
        // A per-scenario client id: an earlier scenario in the run may have
        // bound the fixed field-clock-rig identity, and this scenario's point
        // is the UNLINKED shape (D4: an authenticated principal with no
        // person). sub without | still exercises the D12 rule.
        _machineClientId = $"clock-rig-{Guid.NewGuid():N}";
        _state.LastResponse = await AuthApi.GetAsync(TestJwt.ForMachine(_machineClientId), "/who-am-i");
    }

    [When(@"^the field-clock-rig client captures flightTime of (\d+) seconds on the entry$")]
    public async Task WhenTheMachineClientCapturesFlightTimeOnTheEntry(int seconds)
    {
        _state.LastResponse = await AuthApi.PostAsync(TestJwt.ForMachine(), "/capture-measurement",
            new CaptureMeasurement(_state.EntryId, 1, "flightTime", MeasuredValue.Of((decimal)seconds)));
    }

    [When(@"^Pete registers a person named (.+) and binds the machine identity to it$")]
    public async Task WhenPeteRegistersAPersonAndBindsTheMachineIdentity(string name)
    {
        // Scenario-order independence: the machine identity link is globally
        // unique (the (Provider, Subject) index — the D5 arbiter), so a later
        // scenario must adopt the person an earlier one bound, not rebind.
        // The machine's own /who-am-i is the idempotent way to find out.
        var whoAmI = await AuthApi.GetAsync(TestJwt.ForMachine(), "/who-am-i");
        whoAmI.EnsureSuccessStatusCode();
        var view = await AuthApi.ReadAsync<WhoAmIView>(whoAmI);
        if (view.PersonId is { } alreadyBound)
        {
            _state.MachinePersonId = alreadyBound;
            return;
        }

        var personId = await AuthApi.PostCommandAsync<PersonId>(
            TestJwt.ForPerson(AuthActors.Organiser), "/register-person",
            new RegisterPerson(name, new ContactDetails { Email = $"{AuthActors.MachineClientId}@example.com" }, null));

        (await AuthApi.PostAsync(
                TestJwt.ForPerson(AuthActors.Organiser), "/bind-identity",
                new BindIdentity(personId, "client-credentials", AuthActors.MachineClientId)))
            .EnsureSuccessStatusCode();

        _state.MachinePersonId = personId;
    }

    // ----------------------------------------------------------------- Then

    [Then(@"^the machine is authenticated but linked to nobody and holds no roles$")]
    public async Task ThenTheMachineIsAuthenticatedButLinkedToNobody()
    {
        _state.LastResponse!.EnsureSuccessStatusCode();
        var view = await AuthApi.ReadAsync<WhoAmIView>(_state.LastResponse);

        view.IsAuthenticated.Should().BeTrue("a machine token is an authenticated principal (D12)");
        view.PersonId.Should().BeNull("the machine is nobody until an organiser binds it");
        view.Roles.Should().BeEmpty();
        view.Name.Should().Be(_machineClientId);
    }

    private sealed record WhoAmIView(bool IsAuthenticated, PersonId? PersonId, IReadOnlyList<PersonRole> Roles, string? Name);
}
