using System.Collections.Concurrent;
using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Notifications;

/// <summary>
/// Specifies that concurrent publishes are independent of each other.
/// </summary>
public sealed class NotificationConcurrencyTests
{
    private const int ScopeCount = 1_000;

    [Test]
    public async Task Concurrent_publishes_in_separate_scopes_each_run_their_handlers_in_order()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<OrderRecordingFirstHandler>()
            .AddNotificationHandler<OrderRecordingSecondHandler>());
        ConcurrentDictionary<string, ConcurrentQueue<string>> stepsByMessage = [];
        host.Journal.Capture(stepsByMessage);

        await Parallel.ForEachAsync(Enumerable.Range(0, ScopeCount), async (index, cancellationToken) =>
        {
            await using AsyncServiceScope scope = host.Provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IPublisher>()
                .PublishAsync(new Announcement($"{index}"), cancellationToken);
        });

        await Assert.That(stepsByMessage.Count).IsEqualTo(ScopeCount);
        await Assert.That(stepsByMessage.Values).All(steps => steps.SequenceEqual(["first", "second"]));
    }

    private static ConcurrentQueue<string> StepsFor(Journal journal, Announcement notification)
    {
        ConcurrentDictionary<string, ConcurrentQueue<string>> stepsByMessage =
            journal.Captured<ConcurrentDictionary<string, ConcurrentQueue<string>>>().Single();

        return stepsByMessage.GetOrAdd(notification.Message, _ => new ConcurrentQueue<string>());
    }

    public sealed class OrderRecordingFirstHandler(Journal journal) : INotificationHandler<Announcement>
    {
        public async Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            await Task.Yield();
            StepsFor(journal, notification).Enqueue("first");
        }
    }

    public sealed class OrderRecordingSecondHandler(Journal journal) : INotificationHandler<Announcement>
    {
        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            StepsFor(journal, notification).Enqueue("second");
            return Task.CompletedTask;
        }
    }

}
