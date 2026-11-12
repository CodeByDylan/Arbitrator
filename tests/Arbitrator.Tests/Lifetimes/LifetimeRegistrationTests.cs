using Arbitrator.Tests.Notifications;
using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;

namespace Arbitrator.Tests.Lifetimes;

/// <summary>
/// Specifies how the lifetime overloads record lifetimes, which values they accept, and how lifetimes interact with
/// roles, open behaviors, copies, and container validation.
/// </summary>
public sealed class LifetimeRegistrationTests
{
    private const ServiceLifetime UndefinedLifetime = (ServiceLifetime)42;

    [Test]
    public async Task Overloads_without_a_lifetime_register_transient_components()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>))
            .AddBehavior<SecondBehavior<Ping, Pong>>()
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>))
            .AddHandler<PingHandler>()
            .AddNotificationHandler<FirstAnnouncementHandler>());

        IEnumerable<ComponentRegistration> components = AllComponents(host.Registrations);

        await Assert.That(components.All(component => component.Lifetime == ServiceLifetime.Transient)).IsTrue();
    }

    [Test]
    public async Task Each_overload_records_its_lifetime()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>), ServiceLifetime.Scoped)
            .AddBehavior<SecondBehavior<Ping, Pong>>(ServiceLifetime.Singleton)
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>), ServiceLifetime.Singleton)
            .AddHandler<PingHandler>(ServiceLifetime.Scoped)
            .AddNotificationHandler<FirstAnnouncementHandler>(ServiceLifetime.Singleton));

        RequestRegistration request = host.Registrations.Requests.Single();
        NotificationRegistration notification = host.Registrations.Notifications.Single();

        await Assert.That(request.Handler.Lifetime).IsEqualTo(ServiceLifetime.Scoped);
        await Assert.That(request.Behaviors.Select(behavior => behavior.Lifetime)).IsEquivalentTo(
            [ServiceLifetime.Scoped, ServiceLifetime.Singleton],
            CollectionOrdering.Matching);
        await Assert.That(notification.Handlers.Single().Lifetime).IsEqualTo(ServiceLifetime.Singleton);
        await Assert.That(notification.Behaviors.Single().Lifetime).IsEqualTo(ServiceLifetime.Singleton);
    }

    [Test]
    public async Task Undefined_lifetime_is_rejected_by_every_overload_and_registers_nothing()
    {
        var services = new ServiceCollection();
        services.AddArbitrator(_ => { });
        int descriptorCount = services.Count;
        Action<ArbitratorBuilder>[] registrations =
        [
            arbitrator => arbitrator.AddHandler<PingHandler>(UndefinedLifetime),
            arbitrator => arbitrator.AddNotificationHandler<FirstAnnouncementHandler>(UndefinedLifetime),
            arbitrator => arbitrator.AddBehavior<SecondBehavior<Ping, Pong>>(UndefinedLifetime),
            arbitrator => arbitrator.AddOpenBehavior(typeof(FirstBehavior<,>), UndefinedLifetime),
            arbitrator => arbitrator.AddHandlersFromAssembly(typeof(PingHandler).Assembly, UndefinedLifetime),
            arbitrator => arbitrator.AddHandlersFromAssembly(
                typeof(PingHandler).Assembly,
                _ => true,
                UndefinedLifetime),
            arbitrator => arbitrator.AddHandlersFromAssemblyContaining<PingHandler>(UndefinedLifetime),
            arbitrator => arbitrator.AddHandlersFromAssemblyContaining<PingHandler>(_ => true, UndefinedLifetime),
        ];

        foreach (Action<ArbitratorBuilder> register in registrations)
        {
            ArgumentOutOfRangeException exception = Expect.ThrowsExactly<ArgumentOutOfRangeException>(
                () => services.AddArbitrator(register));
            await Assert.That(exception.ParamName).IsEqualTo("lifetime");
        }

        await Assert.That(services.Count).IsEqualTo(descriptorCount);
    }

    [Test]
    public async Task Class_in_two_roles_keeps_a_lifetime_and_an_instance_per_role()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandler<PingAndAnnouncementHandler>(ServiceLifetime.Singleton)
            .AddNotificationHandler<PingAndAnnouncementHandler>(ServiceLifetime.Scoped));

        await host.Sender.SendAsync(new Ping("hello"));
        await host.Publisher.PublishAsync(new Announcement("hello"));

        await Assert.That(host.Registrations.Requests.Single().Handler.Lifetime).IsEqualTo(ServiceLifetime.Singleton);
        await Assert.That(host.Registrations.Notifications.Single().Handlers.Single().Lifetime)
            .IsEqualTo(ServiceLifetime.Scoped);
        await Assert.That(host.Journal.Captured<PingAndAnnouncementHandler>().Distinct().Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Behavior_lifetime_applies_to_every_instantiation_and_every_role()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>), ServiceLifetime.Singleton)
            .AddBehavior<NotificationBehaviorRegistrationTests.PingAndAnnouncementBehavior>(ServiceLifetime.Scoped)
            .AddHandler<PingHandler>()
            .AddHandler<NotifyHandler>()
            .AddNotificationHandler<FirstAnnouncementHandler>());

        ArbitratorRegistrations registrations = host.Registrations;
        IEnumerable<ServiceLifetime> openBehaviorLifetimes = registrations.Requests
            .Select(request => request.Behaviors[0].Lifetime);

        await Assert.That(openBehaviorLifetimes).IsEquivalentTo([ServiceLifetime.Singleton, ServiceLifetime.Singleton]);
        await Assert.That(registrations.Requests.Single(request => request.RequestType == typeof(Ping)).Behaviors[1]
            .Lifetime).IsEqualTo(ServiceLifetime.Scoped);
        await Assert.That(registrations.Notifications.Single().Behaviors.Single().Lifetime)
            .IsEqualTo(ServiceLifetime.Scoped);
    }

    [Test]
    public async Task Registering_a_type_again_with_another_lifetime_is_a_duplicate()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddHandler<PingHandler>(ServiceLifetime.Scoped)
                .AddHandler<PingHandler>(ServiceLifetime.Singleton)));

        await Assert.That(exception.Message).Contains("is already registered");
    }

    [Test]
    public async Task Copied_collection_keeps_the_recorded_lifetimes()
    {
        var original = new ServiceCollection();
        original.AddSingleton<Journal>();
        original.AddArbitrator(arbitrator => arbitrator.AddHandler<PingHandler>(ServiceLifetime.Singleton));
        IServiceCollection copy = new ServiceCollection();

        foreach (ServiceDescriptor descriptor in original)
        {
            copy.Add(descriptor);
        }

        copy.AddArbitrator(arbitrator => arbitrator.AddHandler<NotifyHandler>(ServiceLifetime.Scoped));
        await using ServiceProvider provider = copy.BuildServiceProvider(TestHost.StrictValidation);

        IEnumerable<ServiceLifetime> lifetimes = provider.GetRequiredService<ArbitratorRegistrations>().Requests
            .Select(request => request.Handler.Lifetime);
        await Assert.That(lifetimes).IsEquivalentTo([ServiceLifetime.Scoped, ServiceLifetime.Singleton]);
    }

    [Test]
    public async Task Lifetime_overloads_are_rejected_on_a_read_only_collection()
    {
        var services = new ServiceCollection();
        services.AddArbitrator(_ => { });
        services.MakeReadOnly();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<PingHandler>(ServiceLifetime.Singleton)));

        await Assert.That(exception.Message).Contains("read-only");
    }

    [Test]
    public async Task Lifetime_overloads_are_rejected_once_the_registrations_are_frozen()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Journal>();
        services.AddArbitrator(arbitrator => arbitrator.AddHandler<PingHandler>());
        await using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<ArbitratorRegistrations>();
        int descriptorCount = services.Count;

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(
                typeof(FirstBehavior<,>),
                ServiceLifetime.Singleton)));

        await Assert.That(exception.Message).Contains("cannot change once");
        await Assert.That(services.Count).IsEqualTo(descriptorCount);
    }

    [Test]
    public async Task Container_validation_reports_singleton_components_with_scoped_dependencies()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedDependency>();
        services.AddArbitrator(arbitrator => arbitrator
            .AddBehavior<CapturingBehavior>(ServiceLifetime.Singleton)
            .AddHandler<CapturingHandler>(ServiceLifetime.Singleton));

        AggregateException exception = Expect.ThrowsExactly<AggregateException>(
            () => services.BuildServiceProvider(TestHost.StrictValidation));

        await Assert.That(exception.Message).Contains(nameof(CapturingHandler));
        await Assert.That(exception.Message).Contains(nameof(CapturingBehavior));
        await Assert.That(exception.Message).Contains("Cannot consume scoped service");
        await Assert.That(exception.Message).Contains(nameof(ScopedDependency));
    }

    private static IEnumerable<ComponentRegistration> AllComponents(ArbitratorRegistrations registrations)
    {
        return
        [
            .. registrations.Requests.SelectMany(request => request.Behaviors.Append(request.Handler)),
            .. registrations.Notifications.SelectMany(
                notification => notification.Behaviors.Concat(notification.Handlers)),
        ];
    }

    public sealed class ScopedDependency;

    public sealed class CapturingHandler(ScopedDependency dependency) : IRequestHandler<Ping, Pong>
    {
        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            GC.KeepAlive(dependency);
            return Task.FromResult(new Pong(request.Message));
        }
    }

    public sealed class CapturingBehavior(ScopedDependency dependency) : IPipelineBehavior<Ping, Pong>
    {
        public Task<Pong> HandleAsync(Ping request, RequestContinuation<Pong> next, CancellationToken cancellationToken)
        {
            GC.KeepAlive(dependency);
            return next(cancellationToken);
        }
    }

    public sealed class PingAndAnnouncementHandler(Journal journal)
        : IRequestHandler<Ping, Pong>, INotificationHandler<Announcement>
    {
        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            journal.Capture(this);
            return Task.FromResult(new Pong(request.Message));
        }

        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            journal.Capture(this);
            return Task.CompletedTask;
        }
    }
}
