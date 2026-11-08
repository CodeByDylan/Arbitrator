using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Notifications;

/// <summary>
/// Specifies how notification behaviors wrap each handler call: order, arguments, applicability, instances,
/// cancellation, and context.
/// </summary>
public sealed class NotificationPipelineTests
{
    [Test]
    public async Task Each_handler_runs_inside_its_own_chain_in_registration_order()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>))
            .AddNotificationHandler<FirstAnnouncementHandler>()
            .AddNotificationHandler<SecondAnnouncementHandler>()
            .AddOpenBehavior(typeof(SecondNotificationBehavior<>)));

        await host.Publisher.PublishAsync(new Announcement("hello"));

        await Expect.EntriesAsync(
            host.Journal,
            "NoticeFirst.before FirstAnnouncementHandler",
            "NoticeSecond.before FirstAnnouncementHandler",
            "first",
            "NoticeSecond.after FirstAnnouncementHandler",
            "NoticeFirst.after FirstAnnouncementHandler",
            "NoticeFirst.before SecondAnnouncementHandler",
            "NoticeSecond.before SecondAnnouncementHandler",
            "second",
            "NoticeSecond.after SecondAnnouncementHandler",
            "NoticeFirst.after SecondAnnouncementHandler");
    }

    [Test]
    public async Task Behaviors_receive_the_notification_and_the_token_and_get_new_instances_per_handler()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>))
            .AddNotificationHandler<FirstAnnouncementHandler>()
            .AddNotificationHandler<SecondAnnouncementHandler>());
        using var cancellation = new CancellationTokenSource();
        var announcement = new Announcement("hello");

        await host.Publisher.PublishAsync(announcement, cancellation.Token);

        bool everyTokenIsTheCallers = host.Journal.Captured<ObservedToken>()
            .All(observed => observed.Token == cancellation.Token);
        bool everyNotificationIsTheCallers = host.Journal.Captured<Announcement>()
            .All(captured => ReferenceEquals(captured, announcement));

        await Assert.That(everyTokenIsTheCallers).IsTrue();
        await Assert.That(everyNotificationIsTheCallers).IsTrue();
        await Assert.That(host.Journal.Captured<FirstNotificationBehavior<Announcement>>().Distinct().Count())
            .IsEqualTo(2);
    }

    [Test]
    public async Task Closed_and_constrained_behaviors_wrap_only_the_notifications_they_apply_to()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<ArchiveOnlyBehavior>()
            .AddOpenBehavior(typeof(AuditedOnlyBehavior<>))
            .AddNotificationHandler<FirstAnnouncementHandler>()
            .AddNotificationHandler<ArchiveHandler>());

        await host.Publisher.PublishAsync(new Announcement("hello"));
        await host.Publisher.PublishAsync(new CustomerArchived());

        await Expect.EntriesAsync(host.Journal, "first", "archive only", "audited", "archived");
    }

    [Test]
    public async Task Request_and_notification_behaviors_never_wrap_each_others_handlers()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>))
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>))
            .AddHandler<PingHandler>()
            .AddNotificationHandler<FirstAnnouncementHandler>());

        await host.Sender.SendAsync(new Ping("hello"));
        await host.Publisher.PublishAsync(new Announcement("hello"));

        await Expect.EntriesAsync(
            host.Journal,
            "First.before",
            "handler",
            "First.after",
            "NoticeFirst.before FirstAnnouncementHandler",
            "first",
            "NoticeFirst.after FirstAnnouncementHandler");
    }

    [Test]
    public async Task Activation_failure_of_a_behavior_runs_nothing()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>))
            .AddNotificationHandler<FirstAnnouncementHandler>()
            .AddNotificationHandler<SecondAnnouncementHandler>()
            .AddBehavior<ExplodingBehavior>());

        await Expect.ThrowsExactlyAsync<InvalidTimeZoneException>(
            () => host.Publisher.PublishAsync(new Announcement("hello")));

        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Token_is_checked_before_each_handlers_chain()
    {
        using var cancellation = new CancellationTokenSource();
        await using TestHost host = TestHost.CreateWithServices(services => services
            .AddSingleton(cancellation)
            .AddArbitrator(arbitrator => arbitrator
                .AddOpenBehavior(typeof(FirstNotificationBehavior<>))
                .AddNotificationHandler<NotificationCancellationTests.CancelingHandler>()
                .AddNotificationHandler<SecondAnnouncementHandler>()));

        await Expect.ThrowsExactlyAsync<OperationCanceledException>(
            () => host.Publisher.PublishAsync(new Announcement("hello"), cancellation.Token));

        await Expect.EntriesAsync(
            host.Journal,
            "NoticeFirst.before CancelingHandler",
            "canceled the publish",
            "NoticeFirst.after CancelingHandler");
    }

    [Test]
    public async Task Publishing_without_handlers_resolves_and_runs_no_behavior()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>))
            .AddNotificationHandler<ArchiveHandler>());

        await host.Publisher.PublishAsync(new Announcement("hello"));

        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    public interface IAudited;

    public sealed record CustomerArchived : INotification, IAudited;

    public sealed class ArchiveHandler(Journal journal) : INotificationHandler<CustomerArchived>
    {
        public Task HandleAsync(CustomerArchived notification, CancellationToken cancellationToken)
        {
            journal.Record("archived");
            return Task.CompletedTask;
        }
    }

    public sealed class ArchiveOnlyBehavior(Journal journal) : INotificationBehavior<CustomerArchived>
    {
        public Task HandleAsync(
            CustomerArchived notification,
            Type handlerType,
            NotificationContinuation next,
            CancellationToken cancellationToken)
        {
            journal.Record("archive only");
            return next(cancellationToken);
        }
    }

    public sealed class AuditedOnlyBehavior<TNotification>(Journal journal) : INotificationBehavior<TNotification>
        where TNotification : INotification, IAudited
    {
        public Task HandleAsync(
            TNotification notification,
            Type handlerType,
            NotificationContinuation next,
            CancellationToken cancellationToken)
        {
            journal.Record("audited");
            return next(cancellationToken);
        }
    }

    public sealed class ExplodingBehavior : INotificationBehavior<Announcement>
    {
        public ExplodingBehavior() => throw new InvalidTimeZoneException();

        public Task HandleAsync(
            Announcement notification,
            Type handlerType,
            NotificationContinuation next,
            CancellationToken cancellationToken) => next(cancellationToken);
    }
}
