using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Lifetimes;

/// <summary>
/// Specifies which instances dispatch uses for each component lifetime: a new one per resolution when transient, one
/// per scope when scoped, and one per provider when singleton.
/// </summary>
public sealed class LifetimeDispatchTests
{
    [Test]
    [Arguments(ServiceLifetime.Transient)]
    [Arguments(ServiceLifetime.Scoped)]
    [Arguments(ServiceLifetime.Singleton)]
    public async Task Request_handler_instances_follow_its_lifetime(ServiceLifetime lifetime)
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<PingHandler>(lifetime));

        await DispatchInTwoScopesAsync(host, services => Sender(services).SendAsync(new Ping("hello")));

        await ExpectIdentityAsync(host.Journal.Captured<PingHandler>(), lifetime);
    }

    [Test]
    [Arguments(ServiceLifetime.Transient)]
    [Arguments(ServiceLifetime.Scoped)]
    [Arguments(ServiceLifetime.Singleton)]
    public async Task Request_behavior_instances_follow_its_lifetime(ServiceLifetime lifetime)
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>), lifetime)
            .AddHandler<PingHandler>());

        await DispatchInTwoScopesAsync(host, services => Sender(services).SendAsync(new Ping("hello")));

        await ExpectIdentityAsync(host.Journal.Captured<FirstBehavior<Ping, Pong>>(), lifetime);
    }

    [Test]
    [Arguments(ServiceLifetime.Transient)]
    [Arguments(ServiceLifetime.Scoped)]
    [Arguments(ServiceLifetime.Singleton)]
    public async Task Stream_handler_and_behavior_instances_follow_their_lifetimes(ServiceLifetime lifetime)
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstStreamBehavior<,>), lifetime)
            .AddHandler<CountdownHandler>(lifetime));

        await DispatchInTwoScopesAsync(
            host,
            services => Streams.ReadAllAsync(Sender(services).StreamAsync(new Countdown(1))));

        await ExpectIdentityAsync(host.Journal.Captured<CountdownHandler>(), lifetime);
        await ExpectIdentityAsync(host.Journal.Captured<FirstStreamBehavior<Countdown, Tick>>(), lifetime);
    }

    [Test]
    [Arguments(ServiceLifetime.Transient)]
    [Arguments(ServiceLifetime.Scoped)]
    [Arguments(ServiceLifetime.Singleton)]
    public async Task Notification_handler_and_behavior_instances_follow_their_lifetimes(ServiceLifetime lifetime)
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>), lifetime)
            .AddNotificationHandler<FirstAnnouncementHandler>(lifetime));

        await DispatchInTwoScopesAsync(host, services => Publisher(services).PublishAsync(new Announcement("hello")));

        await ExpectIdentityAsync(host.Journal.Captured<FirstAnnouncementHandler>(), lifetime);
        await ExpectIdentityAsync(host.Journal.Captured<FirstNotificationBehavior<Announcement>>(), lifetime);
    }

    [Test]
    [Arguments(ServiceLifetime.Transient, 2)]
    [Arguments(ServiceLifetime.Scoped, 1)]
    [Arguments(ServiceLifetime.Singleton, 1)]
    public async Task Notification_behavior_instances_around_the_handlers_of_one_publish_follow_its_lifetime(
        ServiceLifetime lifetime,
        int expectedInstances)
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>), lifetime)
            .AddNotificationHandler<FirstAnnouncementHandler>()
            .AddNotificationHandler<SecondAnnouncementHandler>());

        await host.Publisher.PublishAsync(new Announcement("hello"));

        IReadOnlyList<FirstNotificationBehavior<Announcement>> instances =
            host.Journal.Captured<FirstNotificationBehavior<Announcement>>();
        await Assert.That(instances.Count).IsEqualTo(2);
        await Assert.That(instances.Distinct().Count()).IsEqualTo(expectedInstances);
    }

    [Test]
    [Arguments(ServiceLifetime.Scoped)]
    [Arguments(ServiceLifetime.Singleton)]
    public async Task Behaviors_registered_after_their_handlers_keep_their_lifetime(ServiceLifetime lifetime)
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandler<PingHandler>()
            .AddNotificationHandler<FirstAnnouncementHandler>()
            .AddOpenBehavior(typeof(FirstBehavior<,>), lifetime)
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>), lifetime));

        await DispatchInTwoScopesAsync(host, async services =>
        {
            await Sender(services).SendAsync(new Ping("hello"));
            await Publisher(services).PublishAsync(new Announcement("hello"));
        });

        await ExpectIdentityAsync(host.Journal.Captured<FirstBehavior<Ping, Pong>>(), lifetime);
        await ExpectIdentityAsync(host.Journal.Captured<FirstNotificationBehavior<Announcement>>(), lifetime);
    }

    private static ISender Sender(IServiceProvider services) => services.GetRequiredService<ISender>();

    private static IPublisher Publisher(IServiceProvider services) => services.GetRequiredService<IPublisher>();

    private static async Task DispatchInTwoScopesAsync(TestHost host, Func<IServiceProvider, Task> dispatch)
    {
        await dispatch(host.Scope.ServiceProvider);
        await dispatch(host.Scope.ServiceProvider);

        await using AsyncServiceScope otherScope = host.Provider.CreateAsyncScope();
        await dispatch(otherScope.ServiceProvider);
    }

    private static async Task ExpectIdentityAsync<TComponent>(
        IReadOnlyList<TComponent> instances,
        ServiceLifetime lifetime)
    {
        await Assert.That(instances.Count).IsEqualTo(3);

        bool isSharedWithinScope = ReferenceEquals(instances[0], instances[1]);
        bool isSharedAcrossScopes = ReferenceEquals(instances[0], instances[2]);

        await Assert.That(isSharedWithinScope).IsEqualTo(lifetime != ServiceLifetime.Transient);
        await Assert.That(isSharedAcrossScopes).IsEqualTo(lifetime == ServiceLifetime.Singleton);
    }
}
