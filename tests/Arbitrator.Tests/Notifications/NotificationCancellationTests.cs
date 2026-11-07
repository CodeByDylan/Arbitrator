using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Notifications;

/// <summary>
/// Specifies how cancellation stops publication before and between handlers.
/// </summary>
public sealed class NotificationCancellationTests
{
    [Test]
    public async Task Already_canceled_token_cancels_publishing_before_any_handler_is_constructed()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<ConstructionTrackingHandler>());
        var canceled = new CancellationToken(canceled: true);

        Task publishing = host.Publisher.PublishAsync(new Announcement("hello"), canceled);

        OperationCanceledException exception =
            await Expect.ThrowsExactlyAsync<OperationCanceledException>(() => publishing);
        await Assert.That(publishing.IsCanceled).IsTrue();
        await Assert.That(exception.CancellationToken).IsEqualTo(canceled);
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Cancellation_during_a_handler_stops_the_remaining_handlers()
    {
        using var cancellation = new CancellationTokenSource();
        await using TestHost host = TestHost.CreateWithServices(services => services
            .AddSingleton(cancellation)
            .AddArbitrator(arbitrator => arbitrator
                .AddNotificationHandler<CancelingHandler>()
                .AddNotificationHandler<SecondAnnouncementHandler>()));

        Task publishing = host.Publisher.PublishAsync(new Announcement("hello"), cancellation.Token);

        OperationCanceledException exception =
            await Expect.ThrowsExactlyAsync<OperationCanceledException>(() => publishing);
        await Assert.That(publishing.IsCanceled).IsTrue();
        await Assert.That(exception.CancellationToken).IsEqualTo(cancellation.Token);
        await Expect.EntriesAsync(host.Journal, "canceled the publish");
    }

    [Test]
    public async Task Cancellation_thrown_by_a_handler_propagates_as_the_same_instance()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<GivingUpHandler>()
            .AddNotificationHandler<SecondAnnouncementHandler>());

        OperationCanceledException thrown = await Expect.ThrowsExactlyAsync<OperationCanceledException>(
            () => host.Publisher.PublishAsync(new Announcement("hello")));

        await Assert.That(thrown).IsSameReferenceAs(host.Journal.Captured<OperationCanceledException>().Single());
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    public sealed class ConstructionTrackingHandler : INotificationHandler<Announcement>
    {
        public ConstructionTrackingHandler(Journal journal)
        {
            journal.Record("constructed");
        }

        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class CancelingHandler(CancellationTokenSource cancellation, Journal journal)
        : INotificationHandler<Announcement>
    {
        public async Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            journal.Record("canceled the publish");
            await cancellation.CancelAsync();
        }
    }

    public sealed class GivingUpHandler(Journal journal) : INotificationHandler<Announcement>
    {
        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            var givingUp = new OperationCanceledException("The handler gave up.");
            journal.Capture(givingUp);
            throw givingUp;
        }
    }
}
