using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Notifications;

/// <summary>
/// Specifies how notification registrations relate to service providers, copied collections, and container
/// validation.
/// </summary>
public sealed class NotificationLifecycleTests
{
    [Test]
    public async Task Handler_registered_after_the_provider_was_built_is_reported_when_its_publisher_is_resolved()
    {
        ServiceCollection services = CreateServices();
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<SecondAnnouncementHandler>());
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => scope.ServiceProvider.GetRequiredService<IPublisher>());

        await Assert.That(exception.Message).Contains($"'{Names.Of<SecondAnnouncementHandler>()}'");
        await Assert.That(exception.Message).Contains("registered after this service provider was built");
    }

    [Test]
    public async Task Resolving_the_publisher_freezes_registration()
    {
        ServiceCollection services = CreateServices();
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IPublisher>();
        }

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<SecondAnnouncementHandler>()));

        await Assert.That(exception.Message).Contains("has attempted to resolve ISender or IPublisher");
    }

    [Test]
    public async Task Class_registered_as_notification_handler_after_the_build_is_reported_despite_its_request_role()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Journal>();
        services.AddArbitrator(arbitrator => arbitrator.AddHandler<PingAndAnnouncementHandler>());
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<PingAndAnnouncementHandler>());
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => scope.ServiceProvider.GetRequiredService<IPublisher>());

        await Assert.That(exception.Message).Contains($"'{Names.Of<PingAndAnnouncementHandler>()}'");
    }

    [Test]
    public async Task Resolving_the_sender_freezes_notification_registration()
    {
        ServiceCollection services = CreateServices();
        services.AddArbitrator(arbitrator => arbitrator.AddHandler<PingHandler>());
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ISender>();
        }

        Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<SecondAnnouncementHandler>()));
    }

    [Test]
    public async Task Resolving_the_publisher_freezes_request_registration()
    {
        ServiceCollection services = CreateServices();
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IPublisher>();
        }

        Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<PingHandler>()));
    }

    [Test]
    public async Task Notification_handlers_added_to_a_copied_collection_do_not_reach_the_original()
    {
        ServiceCollection original = CreateServices();
        IServiceCollection copy = new ServiceCollection();
        foreach (ServiceDescriptor descriptor in original)
        {
            copy.Add(descriptor);
        }

        copy.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<SecondAnnouncementHandler>());
        await using ServiceProvider originalProvider = original.BuildServiceProvider(TestHost.StrictValidation);
        await using ServiceProvider copyProvider = copy.BuildServiceProvider(TestHost.StrictValidation);

        await PublishAsync(originalProvider);
        await PublishAsync(copyProvider);

        await Expect.EntriesAsync(originalProvider.GetRequiredService<Journal>(), "first");
        await Expect.EntriesAsync(copyProvider.GetRequiredService<Journal>(), "first", "second");
    }

    [Test]
    public async Task Container_validation_reports_a_notification_handler_with_an_unresolvable_dependency()
    {
        var services = new ServiceCollection();
        services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<HandlerWithMissingDependency>());

        AggregateException exception = Expect.ThrowsExactly<AggregateException>(
            () => services.BuildServiceProvider(TestHost.StrictValidation));

        await Assert.That(exception.Message).Contains(nameof(HandlerWithMissingDependency));
        await Assert.That(exception.Message).Contains(nameof(IMissingDependency));
    }

    [Test]
    public async Task Container_validation_reports_a_singleton_that_captures_the_publisher()
    {
        var services = new ServiceCollection();
        services.AddSingleton<PublisherCapturingSingleton>();
        services.AddArbitrator(_ => { });

        AggregateException exception = Expect.ThrowsExactly<AggregateException>(
            () => services.BuildServiceProvider(TestHost.StrictValidation));

        await Assert.That(exception.Message).Contains("Cannot consume scoped service 'Arbitrator.IPublisher'");
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Journal>();
        services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<FirstAnnouncementHandler>());
        return services;
    }

    private static async Task PublishAsync(ServiceProvider provider)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IPublisher>().PublishAsync(new Announcement("hello"));
    }

    public sealed class PingAndAnnouncementHandler(Journal journal)
        : IRequestHandler<Ping, Pong>, INotificationHandler<Announcement>
    {
        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new Pong(request.Message));
        }

        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            journal.Record("announcement");
            return Task.CompletedTask;
        }
    }

    public interface IMissingDependency;

    public sealed class HandlerWithMissingDependency(IMissingDependency dependency)
        : INotificationHandler<Announcement>
    {
        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            GC.KeepAlive(dependency);
            return Task.CompletedTask;
        }
    }

    public sealed class PublisherCapturingSingleton(IPublisher publisher)
    {
        public IPublisher Publisher { get; } = publisher;
    }
}
