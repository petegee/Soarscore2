using AwesomeAssertions;
using Soarscore.Application.Commands.People;
using Soarscore.Domain.People;
using Xunit;

using Soarscore.Application.Tests.Shared.People;

using Soarscore.Application.Queries.People;
namespace Soarscore.Application.Tests.Commands.People;

public class PersonCommandsTests
{
    private static readonly ContactDetails SampleContact = new() { Email = "pilot@example.com", HomeCity = "Auckland" };

    private static IDispatcher BuildDispatcher(FakeEventStore eventStore, FakeClock clock)
    {
        var services = new Dictionary<Type, object>
        {
            [typeof(ICommandHandler<RegisterPerson, PersonId>)] = new RegisterPersonHandler(eventStore, clock),
            [typeof(ICommandHandler<RenamePerson, PersonId>)] = new RenamePersonHandler(eventStore, clock),
            [typeof(ICommandHandler<ChangePersonContactDetails, PersonId>)] = new ChangePersonContactDetailsHandler(eventStore, clock),
            [typeof(ICommandHandler<ChangePersonClubAffiliation, PersonId>)] = new ChangePersonClubAffiliationHandler(eventStore, clock),
            [typeof(IQueryHandler<GetPerson, Person>)] = new GetPersonHandler(eventStore),
        };
        return new Dispatcher(new FakeServiceProvider(services));
    }

    [Fact]
    public async Task RegisterPerson_appends_a_new_stream_and_returns_its_id()
    {
        var dispatcher = BuildDispatcher(new FakeEventStore(), new FakeClock(DateTimeOffset.UtcNow));

        var result = await dispatcher.SendAsync(new RegisterPerson("Alex Pilot", SampleContact, null), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task RegisterPerson_with_a_blank_name_fails_and_appends_nothing()
    {
        var eventStore = new FakeEventStore();
        var dispatcher = BuildDispatcher(eventStore, new FakeClock(DateTimeOffset.UtcNow));

        var result = await dispatcher.SendAsync(new RegisterPerson("   ", SampleContact, null), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.name.blank");
    }

    [Fact]
    public async Task RegisterPerson_with_an_implausible_email_fails()
    {
        var dispatcher = BuildDispatcher(new FakeEventStore(), new FakeClock(DateTimeOffset.UtcNow));

        var result = await dispatcher.SendAsync(
            new RegisterPerson("Alex Pilot", SampleContact with { Email = "not-an-email" }, null),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.email.invalid");
    }

    [Fact]
    public async Task RenamePerson_after_registration_is_reflected_by_GetPerson()
    {
        var eventStore = new FakeEventStore();
        var dispatcher = BuildDispatcher(eventStore, new FakeClock(DateTimeOffset.UtcNow));
        var registered = await dispatcher.SendAsync(new RegisterPerson("Alex Pilot", SampleContact, null), TestContext.Current.CancellationToken);

        var renamed = await dispatcher.SendAsync(new RenamePerson(registered.Value, "Alexandra Pilot"), TestContext.Current.CancellationToken);
        var fetched = await dispatcher.QueryAsync(new GetPerson(registered.Value), TestContext.Current.CancellationToken);

        renamed.IsSuccess.Should().BeTrue();
        fetched.Value.Name.Should().Be("Alexandra Pilot");
    }

    [Fact]
    public async Task RenamePerson_for_an_unknown_id_fails_with_not_found()
    {
        var dispatcher = BuildDispatcher(new FakeEventStore(), new FakeClock(DateTimeOffset.UtcNow));

        var result = await dispatcher.SendAsync(new RenamePerson(PersonId.New(), "Nobody"), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.notFound");
    }

    [Fact]
    public async Task ChangePersonContactDetails_with_an_implausible_email_fails_and_leaves_the_stream_unappended()
    {
        var eventStore = new FakeEventStore();
        var dispatcher = BuildDispatcher(eventStore, new FakeClock(DateTimeOffset.UtcNow));
        var registered = await dispatcher.SendAsync(new RegisterPerson("Alex Pilot", SampleContact, null), TestContext.Current.CancellationToken);

        var result = await dispatcher.SendAsync(
            new ChangePersonContactDetails(registered.Value, SampleContact with { Email = "not-an-email" }),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.email.invalid");

        var fetched = await dispatcher.QueryAsync(new GetPerson(registered.Value), TestContext.Current.CancellationToken);
        fetched.Value.Contact.Email.Should().Be(SampleContact.Email);
    }

    [Fact]
    public async Task ChangePersonClubAffiliation_to_null_clears_the_club()
    {
        var eventStore = new FakeEventStore();
        var dispatcher = BuildDispatcher(eventStore, new FakeClock(DateTimeOffset.UtcNow));
        var club = new ClubAffiliation { ClubName = "Auckland Soaring Club" };
        var registered = await dispatcher.SendAsync(new RegisterPerson("Alex Pilot", SampleContact, club), TestContext.Current.CancellationToken);

        var result = await dispatcher.SendAsync(new ChangePersonClubAffiliation(registered.Value, null), TestContext.Current.CancellationToken);
        var fetched = await dispatcher.QueryAsync(new GetPerson(registered.Value), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        fetched.Value.Club.Should().BeNull();
    }

    [Fact]
    public async Task A_second_mutation_after_a_first_succeeds_because_it_reads_the_updated_version()
    {
        var eventStore = new FakeEventStore();
        var dispatcher = BuildDispatcher(eventStore, new FakeClock(DateTimeOffset.UtcNow));
        var registered = await dispatcher.SendAsync(new RegisterPerson("Alex Pilot", SampleContact, null), TestContext.Current.CancellationToken);

        var firstRename = await dispatcher.SendAsync(new RenamePerson(registered.Value, "Alexandra Pilot"), TestContext.Current.CancellationToken);
        var secondRename = await dispatcher.SendAsync(new RenamePerson(registered.Value, "Alexandra J. Pilot"), TestContext.Current.CancellationToken);

        firstRename.IsSuccess.Should().BeTrue();
        secondRename.IsSuccess.Should().BeTrue();

        var fetched = await dispatcher.QueryAsync(new GetPerson(registered.Value), TestContext.Current.CancellationToken);
        fetched.Value.Name.Should().Be("Alexandra J. Pilot");
    }
}
