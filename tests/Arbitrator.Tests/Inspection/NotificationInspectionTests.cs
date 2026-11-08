using Arbitrator.Tests.Support;
using TUnit.Assertions.Enums;

namespace Arbitrator.Tests.Inspection;

/// <summary>
/// Specifies how <see cref="ArbitratorRegistrations.Notifications"/> describes notification types and their handlers.
/// </summary>
public sealed class NotificationInspectionTests
{
    [Test]
    public async Task Notification_entry_lists_its_handlers_in_run_order()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<SecondAnnouncementHandler>()
            .AddNotificationHandler<FirstAnnouncementHandler>());

        NotificationRegistration notification = host.Registrations.Notifications.Single();
        await host.Publisher.PublishAsync(new Announcement("hello"));

        await Assert.That(notification.NotificationType).IsEqualTo(typeof(Announcement));
        await Assert.That(notification.HandlerTypes).IsEquivalentTo(
            [typeof(SecondAnnouncementHandler), typeof(FirstAnnouncementHandler)],
            CollectionOrdering.Matching);
        await Expect.EntriesAsync(host.Journal, "second", "first");
    }

    [Test]
    public async Task Handler_of_several_notifications_appears_in_each_entry()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<AuditRecorder>());

        IReadOnlyList<NotificationRegistration> notifications = host.Registrations.Notifications;

        await Assert.That(notifications.Select(notification => notification.NotificationType)).IsEquivalentTo(
            [typeof(CustomerArchived), typeof(Announcement)],
            CollectionOrdering.Matching);
        bool everyEntryListsTheRecorder = notifications.All(
            notification => notification.HandlerTypes.Single() == typeof(AuditRecorder));
        await Assert.That(everyEntryListsTheRecorder).IsTrue();
    }

    [Test]
    public async Task Class_registered_in_both_roles_appears_in_both_lists()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandler<PingAndAnnouncementHandler>()
            .AddNotificationHandler<PingAndAnnouncementHandler>());

        ArbitratorRegistrations registrations = host.Registrations;

        await Assert.That(registrations.Requests.Single().HandlerType).IsEqualTo(typeof(PingAndAnnouncementHandler));
        await Assert.That(registrations.Notifications.Single().HandlerTypes.Single())
            .IsEqualTo(typeof(PingAndAnnouncementHandler));
    }

    public sealed record CustomerArchived : INotification;

    public sealed class AuditRecorder : INotificationHandler<Announcement>, INotificationHandler<CustomerArchived>
    {
        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task HandleAsync(CustomerArchived notification, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    public sealed class PingAndAnnouncementHandler : IRequestHandler<Ping, Pong>, INotificationHandler<Announcement>
    {
        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken) =>
            Task.FromResult(new Pong(request.Message));

        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
