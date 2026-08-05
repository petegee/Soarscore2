// The only routing surface exposed — docs/plans/command-side-steel-thread-plan.md
// WI-8, LADR-0003 "Web / API host": Minimal APIs, MapCommand/MapQuery helpers
// only. Nothing in this project calls MapPost/MapGet/MapPut/etc. directly
// outside these two methods, so registering a non-GET/POST verb is not
// something a later contributor can do by accident — the WI-2 route-shape
// reflection test is the backstop that turns a slip into a failing build.

using Soarscore.Application;
using Soarscore.Domain;

namespace Soarscore.Api.Routing;

public static class EndpointRouteBuilderExtensions
{
    /// <summary>POST — a Command. Binds <typeparamref name="TCommand"/> from the JSON body (never the query string).</summary>
    public static IEndpointRouteBuilder MapCommand<TCommand, TResult>(this IEndpointRouteBuilder endpoints, string path)
        where TCommand : ICommand<TResult>
    {
        endpoints.MapPost(path, async (TCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<TResult>(command, cancellationToken);
            return result.ToHttpResult();
        });

        return endpoints;
    }

    /// <summary>GET — a Query. Binds <typeparamref name="TQuery"/> from the query string via [AsParameters] (never a body).</summary>
    public static IEndpointRouteBuilder MapQuery<TQuery, TResult>(this IEndpointRouteBuilder endpoints, string path)
        where TQuery : IQuery<TResult>
    {
        endpoints.MapGet(path, async ([AsParameters] TQuery query, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.QueryAsync<TResult>(query, cancellationToken);
            return result.ToHttpResult();
        });

        return endpoints;
    }

    /// <summary>
    /// LADR-0003 "Errors": <see cref="Result{T}"/> failures become RFC 9457
    /// ProblemDetails, and this is the one mapping from failure code to status
    /// code. Suffix/exact-matched against the stable codes the WI-4/WI-6/WI-7
    /// layers already produce (Person.cs, PersonLoader.cs, MartenEventStore.cs) —
    /// a later work item's codes fall into the right bucket here without this
    /// file changing, as long as they follow the same "*.notFound" convention.
    /// </summary>
    private static IResult ToHttpResult<T>(this Result<T> result) =>
        result.Match(
            onSuccess: Results.Ok,
            onFailure: failure => Results.Problem(
                statusCode: StatusCodeFor(failure.Code!),
                title: failure.Code,
                detail: failure.Message,
                extensions: failure.Defects.Count == 0
                    ? null
                    : new Dictionary<string, object?> { ["defects"] = failure.Defects }));

    private static int StatusCodeFor(string code) => code switch
    {
        _ when code.EndsWith(".notFound", StringComparison.Ordinal) => StatusCodes.Status404NotFound,
        "eventStore.streamAlreadyExists" => StatusCodes.Status409Conflict,
        "eventStore.concurrencyConflict" => StatusCodes.Status409Conflict,
        "eventStore.uniqueConstraintViolation" => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    };
}
