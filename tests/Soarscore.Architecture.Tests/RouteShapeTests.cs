// WI-2/WI-8 (kanban/completed/command-side-steel-thread-plan.md): turns
// high-level-architecture.md's "intent-based" rule — only POST (a Command) and
// GET (a Query), never PUT/PATCH/DELETE/OPTIONS — into a failing build. Builds
// the real WebApplication via Composition.Build so it exercises the actual
// route table, but never starts Kestrel or opens an HTTP client (LADR-0003
// "Test entry point": driven without HTTP testing tools). The connection
// string is fake and unreachable — Marten only needs it to construct the
// DocumentStore, not to open a connection, since nothing here executes a query.

using AwesomeAssertions;
using Microsoft.AspNetCore.Routing;
using Soarscore.Api;
using Xunit;

namespace Soarscore.ArchitectureTests;

public sealed class RouteShapeTests
{
    [Fact]
    public void Every_endpoint_is_GET_or_POST()
    {
        IEndpointRouteBuilder app = Composition.Build([
            "--ConnectionStrings:Soarscore=Host=127.0.0.1;Port=1;Database=archtest;Username=archtest;Password=archtest",
        ]);

        var methods = app.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
            .Distinct()
            .ToList();

        methods.Should().NotBeEmpty();
        methods.Should().OnlyContain(method => method == "GET" || method == "POST");
    }
}
