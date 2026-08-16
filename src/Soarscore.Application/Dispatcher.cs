// Command/query dispatch — kanban/completed/command-side-steel-thread-plan.md WI-3,
// LADR-0003 "Command/query dispatch".
//
// Hand-rolled over IServiceProvider rather than MediatR (commercial licence
// v12+) or Wolverine (outbox/messaging infrastructure this scale doesn't
// need). No behaviour pipeline, no decorators — every handler call in this
// file is a plain reflective invoke, which is the whole point: it is
// inspectable by reading this file, not by reading a library's source.

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

    private Task<Result<TResult>> Invoke<TResult>(Type openHandlerType, object message, CancellationToken cancellationToken)
    {
        var handlerType = openHandlerType.MakeGenericType(message.GetType(), typeof(TResult));
        var handler = services.GetService(handlerType)
            ?? throw new InvalidOperationException($"No handler registered for {message.GetType().Name} (expected {handlerType.Name}).");

        var handleMethod = handlerType.GetMethod("HandleAsync")
            ?? throw new MissingMethodException(handlerType.FullName, "HandleAsync");

        return (Task<Result<TResult>>)handleMethod.Invoke(handler, [message, cancellationToken])!;
    }
}
