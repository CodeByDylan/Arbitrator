using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Notifications;

/// <summary>
/// Specifies that notification handlers are resolved from, and disposed with, the scope that resolved the publisher.
/// </summary>
public sealed class NotificationScopeTests
{
    [Test]
    public async Task Handler_receives_the_scoped_services_of_the_publishers_scope()
    {
        await using TestHost host = CreateHost();

        await host.Publisher.PublishAsync(new Announcement("hello"));

        ScopedDependency expected = host.Scope.ServiceProvider.GetRequiredService<ScopedDependency>();
        await Assert.That(host.Journal.Captured<ScopedDependency>().Single()).IsSameReferenceAs(expected);
    }

    [Test]
    public async Task Publishing_from_a_request_handler_and_sending_from_a_notification_handler_share_the_scope()
    {
        await using TestHost host = CreateHost();

        await host.Sender.SendAsync(new Deactivate());

        IReadOnlyList<ScopedDependency> dependencies = host.Journal.Captured<ScopedDependency>();
        await Expect.EntriesAsync(host.Journal, "deactivate", "announcement", "audit");
        await Assert.That(dependencies.Distinct().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Disposable_handler_is_disposed_when_its_scope_ends_not_when_publishing_completes()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<DisposableHandler>());
        await using AsyncServiceScope scope = host.Provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IPublisher>().PublishAsync(new Announcement("hello"));
        IReadOnlyList<string> entriesAfterPublish = host.Journal.Entries;
        await scope.DisposeAsync();

        await Assert.That(entriesAfterPublish).IsEquivalentTo(["handled"]);
        await Expect.EntriesAsync(host.Journal, "handled", "disposed");
    }

    private static TestHost CreateHost()
    {
        return TestHost.CreateWithServices(services => services
            .AddScoped<ScopedDependency>()
            .AddArbitrator(arbitrator => arbitrator
                .AddHandler<DeactivateHandler>()
                .AddHandler<AuditHandler>()
                .AddNotificationHandler<AnnouncementHandler>()));
    }

    public sealed class ScopedDependency;

    public sealed record Deactivate : IRequest;

    public sealed record Audit : IRequest;

    public sealed class DeactivateHandler(IPublisher publisher, ScopedDependency dependency, Journal journal)
        : IRequestHandler<Deactivate>
    {
        public async Task HandleAsync(Deactivate request, CancellationToken cancellationToken)
        {
            journal.Record("deactivate");
            journal.Capture(dependency);
            await publisher.PublishAsync(new Announcement("deactivated"), cancellationToken);
        }
    }

    public sealed class AnnouncementHandler(ISender sender, ScopedDependency dependency, Journal journal)
        : INotificationHandler<Announcement>
    {
        public async Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            journal.Record("announcement");
            journal.Capture(dependency);

            if (notification.Message == "deactivated")
            {
                await sender.SendAsync(new Audit(), cancellationToken);
            }
        }
    }

    public sealed class AuditHandler(ScopedDependency dependency, Journal journal) : IRequestHandler<Audit>
    {
        public Task HandleAsync(Audit request, CancellationToken cancellationToken)
        {
            journal.Record("audit");
            journal.Capture(dependency);
            return Task.CompletedTask;
        }
    }

    public sealed class DisposableHandler(Journal journal) : INotificationHandler<Announcement>, IDisposable
    {
        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            journal.Record("handled");
            return Task.CompletedTask;
        }

        public void Dispose() => journal.Record("disposed");
    }
}
