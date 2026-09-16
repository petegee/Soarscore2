// authentication-and-authorisation.md WI-5 — the D1 step in Dispatcher.Invoke,
// tested from the outside: a pipeline denial must return before the handler is
// ever resolved (proved by a spy handler never firing and the event store
// staying empty); an allowance flows exactly as before; and with NO pipeline
// registered the Dispatcher behaves byte-identically to its pre-auth shape
// (DispatcherTests.cs pins that path too and stays untouched).
//
// Real table, real policies, real command types — the only fakes are the
// service provider, the current user, the handler and the store.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Auth;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.People;
using Soarscore.Application.Tests.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Application.Tests.Auth;

public class DispatcherAuthorizationTests
{
    // Deliberately absent from CommandPolicyTable — the fail-closed backstop's
    // proof shape (the totality test in WI-10 makes the backstop unreachable
    // for real messages).
    private sealed record UnmappedProbe(string Text) : ICommand<string>;

    private sealed class SpyRegisterPersonHandler : ICommandHandler<RegisterPerson, PersonId>
    {
        public bool Invoked { get; private set; }

        public Task<Result<PersonId>> HandleAsync(RegisterPerson command, CancellationToken cancellationToken)
        {
            Invoked = true;
            return Task.FromResult(Result<PersonId>.Success(PersonId.New()));
        }
    }

    private static readonly RegisterPerson Command =
        new("Tama Ropata", new ContactDetails { Email = "tama@example.org" }, null);

    private static readonly ICurrentUser Organiser =
        new FakeCurrentUser(IsAuthenticated: true, PersonId: PersonId.New(), HeldRoles: [PersonRole.Organiser]);

    private static readonly ICurrentUser Competitor =
        new FakeCurrentUser(IsAuthenticated: true, PersonId: PersonId.New(), HeldRoles: [PersonRole.Competitor]);

    private static (Dispatcher Dispatcher, SpyRegisterPersonHandler Handler, FakeEventStore Store) Build(
        ICurrentUser user, bool withPipeline, bool withHandler)
    {
        var handler = new SpyRegisterPersonHandler();
        var store = new FakeEventStore();
        var services = new Dictionary<Type, object>
        {
            [typeof(ICommandHandler<RegisterPerson, PersonId>)] = handler,
            [typeof(IEventStore)] = store,
        };
        var provider = new FakeServiceProvider(services);
        if (withPipeline)
        {
            services[typeof(IAuthorizationPipeline)] = new AuthorizationPipeline(user, provider);
        }

        if (!withHandler)
        {
            services.Remove(typeof(ICommandHandler<RegisterPerson, PersonId>));
        }

        return (new Dispatcher(provider), handler, store);
    }

    [Fact]
    public async Task A_pipeline_denial_fails_the_dispatch_without_resolving_or_running_the_handler()
    {
        // No handler registered: if the denial step were skipped, resolution
        // would throw the missing-handler InvalidOperationException instead of
        // returning the policy's failure. An authenticated competitor denies
        // OrganiserPolicy with auth.forbidden (an anonymous principal would
        // 401 first — that arm is pinned in the policy tests).
        var (dispatcher, handler, store) = Build(Competitor, withPipeline: true, withHandler: false);

        var result = await dispatcher.SendAsync(Command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.forbidden");
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_pipeline_denial_never_runs_the_handler_or_touches_the_store()
    {
        var (dispatcher, handler, store) = Build(Competitor, withPipeline: true, withHandler: true);

        var result = await dispatcher.SendAsync(Command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.forbidden");
        handler.Invoked.Should().BeFalse();
        store.Streams.Should().BeEmpty();
    }

    [Fact]
    public async Task A_message_type_absent_from_the_table_fails_closed()
    {
        var (dispatcher, _, store) = Build(Organiser, withPipeline: true, withHandler: false);

        var result = await dispatcher.SendAsync(new UnmappedProbe("hello"), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.policyMissing");
        store.Streams.Should().BeEmpty();
    }

    [Fact]
    public async Task A_query_denial_blocks_its_handler_the_same_way()
    {
        // Queries share Invoke with commands (D1's one step, not two): a real
        // A-mapped query denied by the table's AuthenticatedPolicy must fail
        // without resolving its handler — none is registered here, so a skipped
        // pipeline would surface as the missing-handler throw instead.
        var services = new Dictionary<Type, object>();
        var provider = new FakeServiceProvider(services);
        services[typeof(IAuthorizationPipeline)] = new AuthorizationPipeline(new FakeCurrentUser(), provider);
        var dispatcher = new Dispatcher(provider);

        var result = await dispatcher.QueryAsync(new WhoAmI(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.notAuthenticated");
    }

    [Fact]
    public async Task The_roster_query_is_organiser_only()
    {
        // Security review 2026-09-16 (D4 deviation): FindPeople left the A kind
        // for OrganiserPolicy — an authenticated competitor is a 403, the
        // roster's contact details and roles are not theirs to read.
        var services = new Dictionary<Type, object>();
        var provider = new FakeServiceProvider(services);
        services[typeof(IAuthorizationPipeline)] = new AuthorizationPipeline(Competitor, provider);
        var dispatcher = new Dispatcher(provider);

        var result = await dispatcher.QueryAsync(new FindPeople(null, null), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.forbidden");
    }

    [Fact]
    public async Task A_pipeline_allowance_flows_to_the_handler_exactly_as_before()
    {
        var (dispatcher, handler, store) = Build(Organiser, withPipeline: true, withHandler: true);

        var result = await dispatcher.SendAsync(Command, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        handler.Invoked.Should().BeTrue();
        store.Streams.Should().BeEmpty(); // the spy handler appends nothing; the point is the pipeline ran first
    }

    [Fact]
    public async Task With_no_pipeline_registered_the_dispatch_behaves_exactly_as_before()
    {
        var (dispatcher, handler, _) = Build(new FakeCurrentUser(), withPipeline: false, withHandler: true);

        var result = await dispatcher.SendAsync(Command, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        handler.Invoked.Should().BeTrue();
    }
}
