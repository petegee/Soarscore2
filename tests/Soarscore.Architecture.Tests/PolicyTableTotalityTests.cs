// authentication-and-authorisation.md WI-10 — the totality test for the
// per-command policy table. The AuthorizationPipeline fails closed (a message
// type absent from CommandPolicyTable denies with auth.policyMissing, 500),
// but a fail-closed backstop only surfaces the bug at request time; this test
// makes the backstop unreachable for real messages: every command/query the
// Api maps must have a row in the table, checked at build time.
//
// Reuses HandlerRegistrationTests' route enumeration unchanged — the same
// WebApplication, the same RequestDelegate-metadata reflection — so what it
// sees is exactly what Dispatcher.Invoke will be asked to authorise.

using System.Reflection;
using AwesomeAssertions;
using Microsoft.AspNetCore.Routing;
using Soarscore.Api;
using Soarscore.Application;
using Soarscore.Application.Auth;
using Xunit;

namespace Soarscore.ArchitectureTests;

public sealed class PolicyTableTotalityTests
{
    [Fact]
    public void Every_mapped_command_and_query_has_a_row_in_the_policy_table()
    {
        // D8 (authentication-and-authorisation.md): Production refuses every
        // auth mode but "oidc", so the composition is built in Development —
        // same shape as HandlerRegistrationTests, same unreachable store.
        var app = Composition.Build([
            "--environment=Development",
            "--Soarscore:Store=postgres",
            "--ConnectionStrings:Soarscore=Host=127.0.0.1;Port=1;Database=archtest;Username=archtest;Password=archtest",
        ]);

        IEndpointRouteBuilder endpoints = app;

        var mappedMessages = endpoints.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.Metadata.OfType<MethodInfo>().FirstOrDefault()
                ?.GetParameters().ElementAtOrDefault(0)?.ParameterType)
            .Where(messageType => messageType is not null)
            .Select(messageType => messageType!)
            .Where(messageType => messageType.GetInterfaces().Any(i =>
                (i.IsGenericType && i.GetGenericTypeDefinition() is var generic &&
                    (generic == typeof(ICommand<>) || generic == typeof(IQuery<>)))))
            .Distinct()
            .ToList();

        // Same sanity floor as HandlerRegistrationTests: if the metadata shape
        // changes and the enumeration goes empty, the test must not pass
        // vacuously. Forty-two commands + sixteen queries are mapped as of
        // authentication-and-authorisation.md WI-9.
        mappedMessages.Should().HaveCountGreaterThanOrEqualTo(44);

        var unmapped = mappedMessages
            .Where(messageType => !CommandPolicyTable.Table.ContainsKey(messageType))
            .ToList();

        unmapped.Should().BeEmpty(
            "every mapped message must have a policy row — {0} would fall through to the " +
            "fail-closed auth.policyMissing denial (500) on first request",
            string.Join(", ", unmapped.Select(t => t.Name)));
    }
}
