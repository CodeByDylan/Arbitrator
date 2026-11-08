using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Notifications;

/// <summary>
/// Specifies how publishing uses the caller's synchronization context: every handler starts on it, and nothing
/// returns to it after the last handler.
/// </summary>
public sealed class NotificationContextTests
{
    private static readonly TimeSpan BlockingTimeout = TimeSpan.FromSeconds(10);

    [Test]
    public async Task Every_handler_starts_on_the_synchronization_context_the_caller_published_from()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<YieldingContextHandler>()
            .AddNotificationHandler<ContextObservingHandler>());
        IPublisher publisher = host.Publisher;

        await Task.Run(() => PublishOnContext(publisher, new MarkedSynchronizationContext()));

        await Expect.EntriesAsync(host.Journal, "yielding on context: True", "observing on context: True");
    }

    [Test]
    public async Task Single_handler_publish_completes_for_a_caller_blocking_on_a_single_threaded_context()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<BackgroundCompletingHandler>());

        bool isCompleted = await PublishWhileBlockingAsync(host.Publisher);

        await Assert.That(isCompleted).IsTrue();
        await Expect.EntriesAsync(host.Journal, "completed in the background");
    }

    private static bool IsOnMarkedContext() => MarkedSynchronizationContext.IsCurrent;

    private static Task PublishOnContext(IPublisher publisher, SynchronizationContext context)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);

        try
        {
            return publisher.PublishAsync(new Announcement("hello"));
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static Task<bool> PublishWhileBlockingAsync(IPublisher publisher)
    {
        var outcome = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callerThread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());

            // Deliberately blocks, as a UI thread calling .Wait() would; a regression times out instead of hanging.
            outcome.SetResult(publisher.PublishAsync(new Announcement("hello")).Wait(BlockingTimeout));
        });

        callerThread.Start();
        return outcome.Task;
    }

    public sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            // Never runs posted work, like a single-threaded context whose only thread is blocked.
        }
    }

    public sealed class YieldingContextHandler(Journal journal) : INotificationHandler<Announcement>
    {
        public async Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            journal.Record($"yielding on context: {IsOnMarkedContext()}");
            await Task.Yield();
        }
    }

    public sealed class ContextObservingHandler(Journal journal) : INotificationHandler<Announcement>
    {
        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            journal.Record($"observing on context: {IsOnMarkedContext()}");
            return Task.CompletedTask;
        }
    }

    public sealed class BackgroundCompletingHandler(Journal journal) : INotificationHandler<Announcement>
    {
        public async Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken).ConfigureAwait(false);
            journal.Record("completed in the background");
        }
    }
}
