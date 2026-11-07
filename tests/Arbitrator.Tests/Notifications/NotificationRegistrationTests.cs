using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Notifications;

/// <summary>
/// Specifies notification handler registration and its validation.
/// </summary>
public sealed class NotificationRegistrationTests
{
    [Test]
    public async Task Same_notification_handler_type_registered_twice_is_rejected_without_changing_the_collection()
    {
        var services = new ServiceCollection();
        services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<FirstAnnouncementHandler>());
        int descriptorCount = services.Count;

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<FirstAnnouncementHandler>()));

        await Assert.That(exception.Message)
            .Contains($"Notification handler type '{Names.Of<FirstAnnouncementHandler>()}' is already registered");
        await Assert.That(services.Count).IsEqualTo(descriptorCount);
    }

    [Test]
    public async Task Abstract_and_interface_notification_handlers_are_rejected_without_changing_the_collection()
    {
        var services = new ServiceCollection();
        services.AddArbitrator(_ => { });
        int descriptorCount = services.Count;

        ArbitratorConfigurationException abstractHandler = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<AbstractHandler>()));
        ArbitratorConfigurationException interfaceHandler = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddNotificationHandler<INotificationHandler<Announcement>>()));

        await Assert.That(abstractHandler.Message).Contains("is abstract or an interface");
        await Assert.That(interfaceHandler.Message).Contains("is abstract or an interface");
        await Assert.That(services.Count).IsEqualTo(descriptorCount);
    }

    [Test]
    public async Task Type_without_a_notification_contract_is_rejected_without_changing_the_collection()
    {
        var services = new ServiceCollection();
        services.AddArbitrator(_ => { });
        int descriptorCount = services.Count;

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<PingHandler>()));

        await Assert.That(exception.Message).Contains("cannot be registered as a notification handler");
        await Assert.That(exception.Message).Contains("INotificationHandler<TNotification>");
        await Assert.That(services.Count).IsEqualTo(descriptorCount);
    }

    [Test]
    public async Task Handler_for_an_abstract_notification_type_is_rejected_without_registering_its_other_contracts()
    {
        int descriptorsBefore = 0;
        int descriptorsAfter = 0;

        await using TestHost host = TestHost.CreateWithServices(services =>
        {
            services.AddArbitrator(_ => { });
            descriptorsBefore = services.Count;
            Expect.ThrowsExactly<ArbitratorConfigurationException>(
                () => services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<DomainEventRecorder>()));
            descriptorsAfter = services.Count;
        });

        await host.Publisher.PublishAsync(new Announcement("hello"));

        await Assert.That(descriptorsAfter).IsEqualTo(descriptorsBefore);
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Handler_for_an_abstract_notification_type_names_both_types()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<DomainEventRecorder>()));

        await Assert.That(exception.Message).Contains($"'{Names.Of<DomainEventRecorder>()}'");
        await Assert.That(exception.Message).Contains($"'{Names.Of<DomainEvent>()}'");
        await Assert.That(exception.Message).Contains("could never run");
    }

    [Test]
    public async Task Class_registered_as_request_handler_and_notification_handler_serves_both()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandler<PingAndAnnouncementHandler>()
            .AddNotificationHandler<PingAndAnnouncementHandler>());

        Pong response = await host.Sender.SendAsync(new Ping("hello"));
        await host.Publisher.PublishAsync(new Announcement("hello"));

        await Assert.That(response.Message).IsEqualTo("hello");
        await Expect.EntriesAsync(host.Journal, "ping", "announcement");
    }

    [Test]
    public async Task Notification_handler_on_a_read_only_collection_is_rejected_without_changing_registrations()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Journal>();
        services.AddArbitrator(_ => { });
        services.MakeReadOnly();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<FirstAnnouncementHandler>()));
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IPublisher>().PublishAsync(new Announcement("hello"));

        await Assert.That(exception.Message).Contains("read-only");
        await Assert.That(provider.GetRequiredService<Journal>().Entries).IsEmpty();
    }

    [Test]
    public async Task Type_that_is_both_a_request_and_a_notification_is_sent_and_published_independently()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandler<CheckpointHandler>()
            .AddNotificationHandler<CheckpointObserver>());

        await host.Sender.SendAsync(new Checkpoint());
        await host.Publisher.PublishAsync(new Checkpoint());

        await Expect.EntriesAsync(host.Journal, "checkpoint handled", "checkpoint observed");
    }

    [Test]
    public async Task Repeated_registration_calls_add_one_publisher()
    {
        int publisherDescriptorCount = 0;

        await using TestHost host = TestHost.CreateWithServices(services =>
        {
            services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<FirstAnnouncementHandler>());
            services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<SecondAnnouncementHandler>());
            publisherDescriptorCount = services.Count(descriptor => descriptor.ServiceType == typeof(IPublisher));
        });

        await host.Publisher.PublishAsync(new Announcement("hello"));

        await Assert.That(publisherDescriptorCount).IsEqualTo(1);
        await Expect.EntriesAsync(host.Journal, "first", "second");
    }

    public sealed record Checkpoint : IRequest, INotification;

    public sealed class CheckpointHandler(Journal journal) : IRequestHandler<Checkpoint>
    {
        public Task HandleAsync(Checkpoint request, CancellationToken cancellationToken)
        {
            journal.Record("checkpoint handled");
            return Task.CompletedTask;
        }
    }

    public sealed class CheckpointObserver(Journal journal) : INotificationHandler<Checkpoint>
    {
        public Task HandleAsync(Checkpoint notification, CancellationToken cancellationToken)
        {
            journal.Record("checkpoint observed");
            return Task.CompletedTask;
        }
    }

    public abstract class AbstractHandler : INotificationHandler<Announcement>
    {
        public abstract Task HandleAsync(Announcement notification, CancellationToken cancellationToken);
    }

    public abstract record DomainEvent : INotification;

    public sealed class DomainEventRecorder(Journal journal)
        : INotificationHandler<Announcement>, INotificationHandler<DomainEvent>
    {
        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            journal.Record("announcement");
            return Task.CompletedTask;
        }

        public Task HandleAsync(DomainEvent notification, CancellationToken cancellationToken)
        {
            journal.Record("domain event");
            return Task.CompletedTask;
        }
    }

    public sealed class PingAndAnnouncementHandler(Journal journal)
        : IRequestHandler<Ping, Pong>, INotificationHandler<Announcement>
    {
        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            journal.Record("ping");
            return Task.FromResult(new Pong(request.Message));
        }

        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            journal.Record("announcement");
            return Task.CompletedTask;
        }
    }
}
