using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Notifications;

/// <summary>
/// Specifies which handlers a notification reaches and in what order.
/// </summary>
public sealed class PublishingTests
{
    [Test]
    public async Task Handlers_run_in_registration_order_with_the_callers_notification_and_token()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<FirstAnnouncementHandler>()
            .AddNotificationHandler<SecondAnnouncementHandler>());
        var notification = new Announcement("hello");
        using var cancellation = new CancellationTokenSource();

        await host.Publisher.PublishAsync(notification, cancellation.Token);

        await Expect.EntriesAsync(host.Journal, "first", "second");
        await Assert.That(host.Journal.Captured<Announcement>())
            .All(received => ReferenceEquals(received, notification));
        await Assert.That(host.Journal.Captured<ObservedToken>())
            .All(observed => observed.Token == cancellation.Token);
    }

    [Test]
    public async Task Handler_order_follows_registration_order_across_registration_calls()
    {
        await using TestHost host = TestHost.CreateWithServices(services =>
        {
            services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<SecondAnnouncementHandler>());
            services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<FirstAnnouncementHandler>());
        });

        await host.Publisher.PublishAsync(new Announcement("hello"));

        await Expect.EntriesAsync(host.Journal, "second", "first");
    }

    [Test]
    public async Task Notification_without_handlers_is_published_successfully_even_with_a_canceled_token()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<FirstAnnouncementHandler>());

        await host.Publisher.PublishAsync(new Unheard(), new CancellationToken(canceled: true));

        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Class_handling_two_notification_types_serves_both_with_a_new_instance_per_publish()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<AnnouncementAndUnheardHandler>());

        await host.Publisher.PublishAsync(new Announcement("hello"));
        await host.Publisher.PublishAsync(new Unheard());

        IReadOnlyList<AnnouncementAndUnheardHandler> instances =
            host.Journal.Captured<AnnouncementAndUnheardHandler>();
        await Expect.EntriesAsync(host.Journal, "announcement", "unheard");
        await Assert.That(instances[0]).IsNotSameReferenceAs(instances[1]);
    }

    [Test]
    public async Task Notification_held_through_its_interface_reaches_the_handlers_of_its_runtime_type()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<FirstAnnouncementHandler>());
        INotification notification = new Announcement("hello");

        await host.Publisher.PublishAsync(notification);

        await Expect.EntriesAsync(host.Journal, "first");
    }

    [Test]
    public async Task Derived_notification_does_not_reach_the_handlers_of_its_base_type()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<CustomerEventRecorder>());

        await host.Publisher.PublishAsync(new VipCustomerEvent());

        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Struct_notification_reaches_its_handler_with_its_payload()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<TemperatureHandler>());

        await host.Publisher.PublishAsync(new TemperatureChanged(21));

        await Expect.EntriesAsync(host.Journal, "temperature:21");
    }

    [Test]
    public async Task Request_behaviors_never_wrap_notification_handlers()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>))
            .AddHandler<PingHandler>()
            .AddNotificationHandler<FirstAnnouncementHandler>());

        await host.Publisher.PublishAsync(new Announcement("hello"));

        await Expect.EntriesAsync(host.Journal, "first");
    }

    public sealed record Unheard : INotification;

    public record CustomerEvent : INotification;

    public sealed record VipCustomerEvent : CustomerEvent;

    public readonly record struct TemperatureChanged(int Degrees) : INotification;

    public sealed class AnnouncementAndUnheardHandler(Journal journal)
        : INotificationHandler<Announcement>, INotificationHandler<Unheard>
    {
        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            journal.Record("announcement");
            journal.Capture(this);
            return Task.CompletedTask;
        }

        public Task HandleAsync(Unheard notification, CancellationToken cancellationToken)
        {
            journal.Record("unheard");
            journal.Capture(this);
            return Task.CompletedTask;
        }
    }

    public sealed class CustomerEventRecorder(Journal journal) : RecordingNotificationHandler<CustomerEvent>(
        journal,
        "customer event");

    public sealed class TemperatureHandler(Journal journal) : INotificationHandler<TemperatureChanged>
    {
        public Task HandleAsync(TemperatureChanged notification, CancellationToken cancellationToken)
        {
            journal.Record($"temperature:{notification.Degrees}");
            return Task.CompletedTask;
        }
    }
}
