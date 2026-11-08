using Arbitrator.Tests.Scanning;
using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Inspection;

/// <summary>
/// Specifies how <see cref="ArbitratorRegistrations"/> is resolved, when it freezes the registrations, and that it
/// can't be changed.
/// </summary>
public sealed class InspectionLifecycleTests
{
    [Test]
    public async Task Resolves_from_the_root_and_from_a_scope_as_the_same_instance()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<PingHandler>());

        ArbitratorRegistrations fromRoot = host.Provider.GetRequiredService<ArbitratorRegistrations>();
        ArbitratorRegistrations fromScope = host.Scope.ServiceProvider.GetRequiredService<ArbitratorRegistrations>();

        await Assert.That(fromScope).IsSameReferenceAs(fromRoot);
    }

    [Test]
    public async Task Resolving_it_freezes_the_registrations()
    {
        ServiceCollection services = CreateServices(arbitrator => arbitrator.AddHandler<PingHandler>());
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        provider.GetRequiredService<ArbitratorRegistrations>();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<NotifyHandler>()));

        await Assert.That(exception.Message).Contains("cannot change once");
    }

    [Test]
    public async Task Building_with_container_validation_does_not_freeze_the_registrations()
    {
        ServiceCollection services = CreateServices(arbitrator => arbitrator.AddHandler<PingHandler>());
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);

        services.AddArbitrator(arbitrator => arbitrator.AddHandler<NotifyHandler>());

        await Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(NotifyHandler))).IsEqualTo(1);
    }

    [Test]
    public async Task Resolution_fails_like_the_sender_for_a_closed_behavior_without_a_handler()
    {
        ServiceCollection services = CreateServices(arbitrator => arbitrator
            .AddBehavior<OrphanBehavior>()
            .AddHandler<PingHandler>());
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        ArbitratorConfigurationException fromInspection = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => provider.GetRequiredService<ArbitratorRegistrations>());
        ArbitratorConfigurationException fromSender = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => scope.ServiceProvider.GetRequiredService<ISender>());

        await Assert.That(fromInspection.Message).IsEqualTo(fromSender.Message);
        await Assert.That(fromInspection.Message).Contains(Names.Of<OrphanBehavior>());
    }

    [Test]
    public async Task Resolution_fails_for_registrations_made_after_the_provider_was_built()
    {
        ServiceCollection services = CreateServices(arbitrator => arbitrator.AddHandler<PingHandler>());
        await using ServiceProvider provider = services.BuildServiceProvider();
        services.AddArbitrator(arbitrator => arbitrator.AddHandler<NotifyHandler>());

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => provider.GetRequiredService<ArbitratorRegistrations>());

        await Assert.That(exception.Message).Contains("registered after this service provider was built");
    }

    [Test]
    public async Task Providers_built_from_one_collection_return_the_same_description()
    {
        ServiceCollection services = CreateServices(arbitrator => arbitrator.AddHandler<PingHandler>());
        await using ServiceProvider first = services.BuildServiceProvider();
        await using ServiceProvider second = services.BuildServiceProvider();

        await Assert.That(second.GetRequiredService<ArbitratorRegistrations>())
            .IsSameReferenceAs(first.GetRequiredService<ArbitratorRegistrations>());
    }

    [Test]
    public async Task Copied_collection_describes_only_its_own_registrations()
    {
        ServiceCollection original = CreateServices(arbitrator => arbitrator.AddHandler<PingHandler>());
        IServiceCollection copy = new ServiceCollection();

        foreach (ServiceDescriptor descriptor in original)
        {
            copy.Add(descriptor);
        }

        copy.AddArbitrator(arbitrator => arbitrator.AddHandler<NotifyHandler>());
        await using ServiceProvider originalProvider = original.BuildServiceProvider();
        await using ServiceProvider copyProvider = copy.BuildServiceProvider();

        await Assert.That(RequestTypes(originalProvider)).IsEquivalentTo([typeof(Ping)]);
        await Assert.That(RequestTypes(copyProvider)).IsEquivalentTo([typeof(Notify), typeof(Ping)]);
    }

    [Test]
    public async Task Scanned_handlers_are_described()
    {
        StubAssembly assembly = StubAssembly.Defining(
            "Contoso.Inspection",
            typeof(PingHandler),
            typeof(AnnouncementRecorder));

        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandlersFromAssembly(assembly));

        await Assert.That(host.Registrations.Requests.Single().HandlerType).IsEqualTo(typeof(PingHandler));
        await Assert.That(host.Registrations.Notifications.Single().HandlerTypes.Single())
            .IsEqualTo(typeof(AnnouncementRecorder));
    }

    [Test]
    public async Task Without_registrations_both_lists_are_empty()
    {
        await using TestHost host = TestHost.Create(_ => { });

        await Assert.That(host.Registrations.Requests).IsEmpty();
        await Assert.That(host.Registrations.Notifications).IsEmpty();
    }

    [Test]
    public async Task Lists_are_read_only()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandler<PingHandler>()
            .AddNotificationHandler<AnnouncementRecorder>());
        ArbitratorRegistrations registrations = host.Registrations;

        Expect.ThrowsExactly<NotSupportedException>(() => ((IList<RequestRegistration>)registrations.Requests).Clear());
        Expect.ThrowsExactly<NotSupportedException>(
            () => ((IList<NotificationRegistration>)registrations.Notifications).Clear());
        Expect.ThrowsExactly<NotSupportedException>(
            () => ((IList<Type>)registrations.Requests[0].BehaviorTypes).Add(typeof(object)));
        Expect.ThrowsExactly<NotSupportedException>(
            () => ((IList<Type>)registrations.Notifications[0].HandlerTypes).Add(typeof(object)));

        await Assert.That(registrations.Requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Inspecting_constructs_no_handler()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandler<ConstructionRecordingHandler>()
            .AddNotificationHandler<AnnouncementRecorder>());

        ArbitratorRegistrations registrations = host.Registrations;

        await Assert.That(registrations.Requests.Single().HandlerType).IsEqualTo(typeof(ConstructionRecordingHandler));
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    private static ServiceCollection CreateServices(Action<ArbitratorBuilder> configure)
    {
        ServiceCollection services = [];
        services.AddSingleton<Journal>();
        services.AddArbitrator(configure);
        return services;
    }

    private static IEnumerable<Type> RequestTypes(IServiceProvider provider)
    {
        return provider.GetRequiredService<ArbitratorRegistrations>().Requests.Select(request => request.RequestType);
    }

    public sealed class OrphanBehavior : IPipelineBehavior<Notify, Unit>
    {
        public Task<Unit> HandleAsync(
            Notify request,
            RequestContinuation<Unit> next,
            CancellationToken cancellationToken)
        {
            return next(cancellationToken);
        }
    }

    public sealed class AnnouncementRecorder(Journal journal)
        : RecordingNotificationHandler<Announcement>(journal, "recorder");

    public sealed class ConstructionRecordingHandler : IRequestHandler<Ping, Pong>
    {
        public ConstructionRecordingHandler(Journal journal)
        {
            journal.Record("constructed");
        }

        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken) =>
            Task.FromResult(new Pong(request.Message));
    }
}
