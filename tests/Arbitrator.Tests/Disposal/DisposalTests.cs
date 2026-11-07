using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Disposal;

/// <summary>
/// Specifies that the container, not Arbitrator, disposes handlers and behaviors when their scope ends.
/// </summary>
public sealed class DisposalTests
{
    [Test]
    public async Task Disposable_handler_is_disposed_once_when_its_scope_ends_not_when_the_send_completes()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<DisposableHandler>());
        await using AsyncServiceScope scope = host.Provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new Ping("hello"));
        IReadOnlyList<string> entriesAfterSend = host.Journal.Entries;
        await scope.DisposeAsync();

        await Assert.That(entriesAfterSend).IsEquivalentTo(["handler"]);
        await Expect.EntriesAsync(host.Journal, "handler", "handler disposed");
    }

    [Test]
    public async Task Asynchronously_disposable_behavior_is_disposed_when_its_scope_is_disposed_asynchronously()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<AsyncDisposableBehavior>()
            .AddHandler<PingHandler>());
        await using AsyncServiceScope scope = host.Provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new Ping("hello"));
        await scope.DisposeAsync();

        await Expect.EntriesAsync(host.Journal, "handler", "behavior disposed");
    }

    [Test]
    public async Task Scope_remains_usable_after_a_failed_send_and_nothing_is_disposed_early()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandler<DisposableFailingHandler>()
            .AddHandler<PingHandler>());
        await using AsyncServiceScope scope = host.Provider.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => sender.SendAsync(new Notify("fail")));
        await sender.SendAsync(new Ping("hello"));
        IReadOnlyList<string> entriesBeforeDisposal = host.Journal.Entries;
        await scope.DisposeAsync();

        await Assert.That(entriesBeforeDisposal).IsEquivalentTo(["handler"]);
        await Expect.EntriesAsync(host.Journal, "handler", "failing handler disposed");
    }

    public sealed class DisposableHandler(Journal journal) : IRequestHandler<Ping, Pong>, IDisposable
    {
        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            journal.Record("handler");
            return Task.FromResult(new Pong(request.Message));
        }

        public void Dispose() => journal.Record("handler disposed");
    }

    public sealed class AsyncDisposableBehavior(Journal journal) : IPipelineBehavior<Ping, Pong>, IAsyncDisposable
    {
        public Task<Pong> HandleAsync(Ping request, RequestContinuation<Pong> next, CancellationToken cancellationToken)
        {
            return next(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            journal.Record("behavior disposed");
            return ValueTask.CompletedTask;
        }
    }

    public sealed class DisposableFailingHandler(Journal journal) : IRequestHandler<Notify>, IDisposable
    {
        public Task HandleAsync(Notify request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The handler failed.");
        }

        public void Dispose() => journal.Record("failing handler disposed");
    }
}
