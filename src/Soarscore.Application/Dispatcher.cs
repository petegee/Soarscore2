// Command/query dispatch — kanban/completed/command-side-steel-thread-plan.md WI-3,
// LADR-0003 "Command/query dispatch".
//
// Hand-rolled over IServiceProvider rather than MediatR (commercial licence
// v12+) or Wolverine (outbox/messaging infrastructure this scale doesn't
// need). No behaviour pipeline, no decorators — every handler call in this
// file is a plain reflective invoke, which is the whole point: it is
// inspectable by reading this file, not by reading a library's source.
//
// The one behaviour step (added by authentication-and-authorisation.md WI-5,
// owner decision 2026-09-14 — D1): before resolving a handler the Dispatcher
// consults IAuthorizationPipeline when one is registered. Under none-mode no
// pipeline is registered and this file behaves exactly as it always did; when
// one is present, a denial maps straight to Result<T>.Failure without
// resolving the handler. The original "no behaviour pipeline" claim is
// amended rather than withdrawn: this is the single behaviour step, it is the
// owner-decided enforcement point, and it is inspectable by reading the
// pipeline class (AuthorizationPipeline.cs), not a library's source.

using Soarscore.Application.Auth;
using Soarscore.Domain;

namespace Soarscore.Application;

public interface ICommand<TResult>;

public interface IQuery<TResult>;

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

public interface IDispatcher
{
    Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);

    Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}

public sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    public Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default) =>
        Invoke<TResult>(typeof(ICommandHandler<,>), command, cancellationToken);

    public Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) =>
        Invoke<TResult>(typeof(IQueryHandler<,>), query, cancellationToken);

    private async Task<Result<TResult>> Invoke<TResult>(Type openHandlerType, object message, CancellationToken cancellationToken)
    {
        // D1: enforcement is opt-in per composition. Absent pipeline (none-mode,
        // every bare-Dispatcher test) ⇒ exactly the pre-auth path; present ⇒ a
        // denial returns before the handler is ever resolved, so an unauthorised
        // caller cannot reach — let alone run — a handler.
        if (services.GetService(typeof(IAuthorizationPipeline)) is IAuthorizationPipeline pipeline)
        {
            var outcome = await pipeline.AuthorizeAsync(message, cancellationToken);
            if (!outcome.Allowed)
            {
                return Result<TResult>.Failure(outcome.Code!, outcome.Message!);
            }
        }

        var handlerType = openHandlerType.MakeGenericType(message.GetType(), typeof(TResult));
        var handler = services.GetService(handlerType)
            ?? throw new InvalidOperationException($"No handler registered for {message.GetType().Name} (expected {handlerType.Name}).");

        var handleMethod = handlerType.GetMethod("HandleAsync")
            ?? throw new MissingMethodException(handlerType.FullName, "HandleAsync");

        var task = (Task<Result<TResult>>)handleMethod.Invoke(handler, [message, cancellationToken])!;
        return await task;
    }
}
