// authentication-and-authorisation.md WI-7 (D12). Covers BindIdentityHandler
// directly against the People fakes: the organiser's provisioning path binds
// any external identity — an interactive provider account or a
// client-credentials client id — to an existing person. Blank provider or
// subject is the domain decide's refusal (person.identity.blank), surfaced
// verbatim: the handler does not pre-check, the decide is the single
// validator.

using AwesomeAssertions;
using Soarscore.Application.Commands.People;
using Soarscore.Domain;
using Soarscore.Domain.People;
using Xunit;

using Soarscore.Application.Tests.Shared.People;

namespace Soarscore.Application.Tests.Commands.People;

public class BindIdentityHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    private static (FakeEventStore Store, PersonId PersonId) SeedPerson()
    {
        var store = new FakeEventStore();
        var id = PersonId.New();
        store.AppendAsync(id.Value, ExpectedVersion.NoStream,
        [
            new PersonRegistered(id, "Field Clock Rig", new ContactDetails { Email = "rig@example.org" }, null, Now),
        ]).GetAwaiter().GetResult();
        return (store, id);
    }

    private static IReadOnlyList<IDomainEvent> Stream(FakeEventStore store, Guid streamId) =>
        store.ReadStreamAsync(streamId, 0).GetAwaiter().GetResult().Value;

    [Fact]
    public async Task Binding_an_identity_appends_IdentityLinked_at_the_next_version()
    {
        var (store, personId) = SeedPerson();
        var handler = new BindIdentityHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new BindIdentity(personId, "client-credentials", "field-rig-client"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(personId);

        var stream = Stream(store, personId.Value);
        stream.Should().HaveCount(2);
        var linked = stream[1].Should().BeOfType<IdentityLinked>().Subject;
        linked.Provider.Should().Be("client-credentials");   // D12: a machine actor's provider is the client-credentials pseudo-provider
        linked.Subject.Should().Be("field-rig-client");
    }

    [Fact]
    public async Task A_blank_provider_is_the_domain_decides_refusal()
    {
        var (store, personId) = SeedPerson();
        var handler = new BindIdentityHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(new BindIdentity(personId, "  ", "field-rig-client"), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.identity.blank");
        Stream(store, personId.Value).Should().HaveCount(1);
    }

    [Fact]
    public async Task A_blank_subject_is_the_domain_decides_refusal()
    {
        var (store, personId) = SeedPerson();
        var handler = new BindIdentityHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(new BindIdentity(personId, "auth0", ""), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.identity.blank");
        Stream(store, personId.Value).Should().HaveCount(1);
    }

    [Fact]
    public async Task Binding_to_an_unknown_person_fails_notFound()
    {
        var handler = new BindIdentityHandler(new FakeEventStore(), new FakeClock(Now));

        var result = await handler.HandleAsync(
            new BindIdentity(PersonId.New(), "auth0", "sub-1"), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.notFound");
    }
}