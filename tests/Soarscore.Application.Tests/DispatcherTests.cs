using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Domain;
using Xunit;

namespace Soarscore.Application.Tests;

public class DispatcherTests
{
    private sealed record Echo(string Text) : ICommand<string>;

    private sealed class EchoHandler : ICommandHandler<Echo, string>
    {
        public Task<Result<string>> HandleAsync(Echo command, CancellationToken cancellationToken) =>
            Task.FromResult(Result<string>.Success(command.Text));
    }

    private sealed record CountLetters(string Text) : IQuery<int>;

    private sealed class CountLettersHandler : IQueryHandler<CountLetters, int>
    {
        public Task<Result<int>> HandleAsync(CountLetters query, CancellationToken cancellationToken) =>
            Task.FromResult(Result<int>.Success(query.Text.Length));
    }

    /// <summary>Hand-written fake (LADR-0003 "Doubles") — a real DI container is Infrastructure/Api's composition-root concern, not something this port test needs.</summary>
    private sealed class FakeServiceProvider(Dictionary<Type, object> services) : IServiceProvider
    {
        public object? GetService(Type serviceType) => services.GetValueOrDefault(serviceType);
    }

    [Fact]
    public async Task SendAsync_resolves_and_invokes_the_registered_command_handler()
    {
        var services = new Dictionary<Type, object> { [typeof(ICommandHandler<Echo, string>)] = new EchoHandler() };
        IDispatcher dispatcher = new Dispatcher(new FakeServiceProvider(services));

        var result = await dispatcher.SendAsync(new Echo("hello"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public async Task QueryAsync_resolves_and_invokes_the_registered_query_handler()
    {
        var services = new Dictionary<Type, object> { [typeof(IQueryHandler<CountLetters, int>)] = new CountLettersHandler() };
        IDispatcher dispatcher = new Dispatcher(new FakeServiceProvider(services));

        var result = await dispatcher.QueryAsync(new CountLetters("glider"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(6);
    }

    [Fact]
    public async Task SendAsync_with_no_registered_handler_throws()
    {
        IDispatcher dispatcher = new Dispatcher(new FakeServiceProvider(new Dictionary<Type, object>()));

        await FluentActions.Awaiting(() => dispatcher.SendAsync(new Echo("hello")))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}
