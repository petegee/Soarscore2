// WI-11 (kanban/completed/capture-a-score-steel-thread-plan.md). The gap this
// closes: RouteShapeTests only reflects over route *shape* (path/verb) and cannot see
// that a mapped command or query has no matching DI registration — that class
// of bug currently surfaces as a 500 on first real request, caught only by
// manual review (found by the bind-parameter thread's near-miss).
//
// Builds the same WebApplication RouteShapeTests builds (Composition.Build),
// then finds every mapped command/query type without touching Commands.cs,
// Queries.cs or Composition.cs: MapCommand<TCommand,TResult> and
// MapQuery<TQuery,TResult> (Routing/EndpointRouteBuilderExtensions.cs) each
// register a compiler-generated lambda — (TMessage, IDispatcher,
// CancellationToken) — as the endpoint's RequestDelegate, and ASP.NET Core
// attaches that lambda's MethodInfo as endpoint metadata regardless of verb.
// Its first parameter is always the concrete command/query type, so that
// MethodInfo is the one reflection point common to both MapCommand (which
// also gets AcceptsMetadata, body-only) and MapQuery (which does not).
//
// For each message type found this way, the test derives the handler
// interface exactly as Dispatcher.Invoke does — ICommand<TResult> /
// IQuery<TResult> on the message gives TResult, which closes
// ICommandHandler<,>/IQueryHandler<,> — and asserts it resolves from the
// built IServiceProvider. A route with no matching AddScoped in
// Composition.cs fails this test with the offending route and message type
// named, instead of failing silently until the first real request.

using System.Reflection;
using AwesomeAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Soarscore.Api;
using Soarscore.Application;
using Xunit;

namespace Soarscore.ArchitectureTests;

public sealed class HandlerRegistrationTests
{
    [Fact]
    public void Every_mapped_command_and_query_resolves_its_handler_from_DI()
    {
        var app = Composition.Build([
            "--ConnectionStrings:Soarscore=Host=127.0.0.1;Port=1;Database=archtest;Username=archtest;Password=archtest",
        ]);

        IEndpointRouteBuilder endpoints = app;

        // Only endpoints whose first parameter is itself an ICommand<>/IQuery<>
        // are ours — this is exactly the generic constraint MapCommand and
        // MapQuery enforce at compile time, so it isolates their endpoints
        // from anything else mapped onto the same WebApplication (MapOpenApi
        // registers an endpoint too, taking a bare HttpContext).
        var mappedMessages = endpoints.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => new
            {
                Route = endpoint.RoutePattern.RawText,
                MessageType = endpoint.Metadata.OfType<MethodInfo>().FirstOrDefault()
                    ?.GetParameters().ElementAtOrDefault(0)?.ParameterType,
            })
            .Where(x => x.MessageType is not null)
            .Select(x => new { x.Route, MessageType = x.MessageType!, HandlerType = HandlerInterfaceFor(x.MessageType!) })
            .Where(x => x.HandlerType is not null)
            .Select(x => new { x.Route, x.MessageType, HandlerType = x.HandlerType! })
            .ToList();

        // Sanity check on the reflection technique itself: if this is empty,
        // the metadata shape MapCommand/MapQuery rely on has changed and the
        // test below would vacuously pass. Thirty-four commands + thirteen
        // queries are mapped as of teams-mvp.md WI-6 (the seven team commands
        // and three team queries — the count comment here previously said
        // twenty-seven commands + ten queries as of lane-assignment.md WI-3;
        // corrected while bumping, the smaller-items.md precedent).
        mappedMessages.Should().HaveCountGreaterThanOrEqualTo(38);

        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        foreach (var mapped in mappedMessages)
        {
            services.GetService(mapped.HandlerType).Should().NotBeNull(
                $"{mapped.Route} maps {mapped.MessageType.Name} but no {mapped.HandlerType.Name} " +
                "is registered in Composition.cs (AddScoped is missing)");
        }
    }

    /// <summary>
    /// Mirrors Dispatcher.Invoke's own derivation (Dispatcher.cs): the
    /// message's own ICommand&lt;TResult&gt;/IQuery&lt;TResult&gt; interface
    /// supplies TResult, which closes ICommandHandler&lt;,&gt; /
    /// IQueryHandler&lt;,&gt; — the exact type the dispatcher asks
    /// IServiceProvider for at request time.
    /// </summary>
    private static Type? HandlerInterfaceFor(Type messageType)
    {
        var commandInterface = messageType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));
        if (commandInterface is not null)
        {
            return typeof(ICommandHandler<,>).MakeGenericType(messageType, commandInterface.GetGenericArguments()[0]);
        }

        var queryInterface = messageType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));
        if (queryInterface is not null)
        {
            return typeof(IQueryHandler<,>).MakeGenericType(messageType, queryInterface.GetGenericArguments()[0]);
        }

        return null;
    }
}
