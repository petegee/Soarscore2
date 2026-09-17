// The only routing surface exposed — kanban/completed/command-side-steel-thread-plan.md
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
    /// Success with advisories (kanban/in-progress
    /// /should-level-minima-warn-dont-refuse.md WI-2) stays 200; the value is
    /// carried beside the warnings in one envelope, the same defects-style
    /// extension mechanism failures use. Empty advisories return the value
    /// bare, byte-identical to before.
    /// </summary>
    private static IResult ToHttpResult<T>(this Result<T> result) =>
        result.Match(
            onSuccess: value => result.Advisories.Count == 0
                ? Results.Ok(value)
                : Results.Json(new { value, warnings = result.Advisories }),
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
        // authentication-and-authorisation.md WI-9 step 8. auth.policyMissing
        // is 500 deliberately: the pipeline fails closed (a message type with
        // no policy row is a wiring bug, and the WI-10 totality test should
        // make it unreachable — a 400 would read as the caller's fault).
        "auth.notAuthenticated" => StatusCodes.Status401Unauthorized,
        "auth.forbidden" => StatusCodes.Status403Forbidden,
        "auth.capturePolicy.denied" => StatusCodes.Status403Forbidden,
        // Security review 2026-09-16. 403 not 401: the identity itself
        // validated — what is untrustworthy is the token's unverified email
        // claim, which is the caller's token to fix at the IdP.
        "auth.signIn.emailNotVerified" => StatusCodes.Status403Forbidden,
        // Security review 2026-09-17 (secure-automatic-identity-linking.md).
        // 403 not 401: the identity is valid — what is refused is the email
        // the command carries, which the caller can fix (keep the stored
        // address, or verify the new one at the IdP) or delegate to an
        // organiser.
        "auth.contact.emailOwnership" => StatusCodes.Status403Forbidden,
        // 409 not 404/403: the verified email matched a real person that
        // already holds a linked sign-in — automatic linking would merge
        // accounts, so it is refused; an organiser binds the identity
        // explicitly (/bind-identity).
        "auth.signIn.explicitLinkRequired" => StatusCodes.Status409Conflict,
        "auth.policyMissing" => StatusCodes.Status500InternalServerError,
        "eventStore.streamAlreadyExists" => StatusCodes.Status409Conflict,
        "eventStore.concurrencyConflict" => StatusCodes.Status409Conflict,
        "eventStore.uniqueConstraintViolation" => StatusCodes.Status409Conflict,
        "person.lastOrganiser" => StatusCodes.Status409Conflict,
        "person.roleAlreadyHeld" => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    };
}
