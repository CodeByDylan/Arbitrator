using Arbitrator.Tests.Scanning.Fixtures;
using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Scanning;

/// <summary>
/// Specifies that an assembly scan follows the registration rules of explicit registrations: atomicity, duplicates,
/// freezing, read-only collections, argument checks, and copied collections.
/// </summary>
public sealed class ScanningRegistrationRulesTests
{
    [Test]
    public async Task Exception_from_the_filter_propagates_unchanged_and_nothing_is_registered()
    {
        ServiceCollection services = CreateServices();
        int descriptorCount = services.Count;
        var failure = new InvalidOperationException("The filter failed.");

        InvalidOperationException exception = Expect.ThrowsExactly<InvalidOperationException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddHandlersFromAssemblyContaining<ScanQuery>(_ => throw failure)));
        int descriptorCountAfterFailure = services.Count;
        services.AddArbitrator(arbitrator => arbitrator
            .AddHandlersFromAssemblyContaining<ScanQuery>(ScanFixtures.Contains));

        await Assert.That(exception).IsSameReferenceAs(failure);
        await Assert.That(descriptorCountAfterFailure).IsEqualTo(descriptorCount);
        await Assert.That(services.Count).IsGreaterThan(descriptorCount);
    }

    [Test]
    public async Task Scan_that_fails_on_a_later_type_registers_nothing()
    {
        StubAssembly assembly = StubAssembly.Defining(
            "Contoso.Partial",
            typeof(ScanQueryHandler),
            typeof(AlphaNoticeHandler),
            typeof(InterfaceNotificationHandler));
        ServiceCollection services = CreateServices();
        int descriptorCount = services.Count;

        Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandlersFromAssembly(assembly)));
        int descriptorCountAfterFailure = services.Count;
        services.AddArbitrator(arbitrator => arbitrator
            .AddHandler<ScanQueryHandler>()
            .AddNotificationHandler<AlphaNoticeHandler>());
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        string response = await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new ScanQuery("hi"));

        await Assert.That(descriptorCountAfterFailure).IsEqualTo(descriptorCount);
        await Assert.That(response).IsEqualTo("hi");
    }

    [Test]
    public async Task Scanning_a_handler_type_that_is_already_registered_is_rejected()
    {
        ServiceCollection services = CreateServices();
        services.AddArbitrator(arbitrator => arbitrator.AddHandler<ScanQueryHandler>());

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddHandlersFromAssemblyContaining<ScanQuery>(ScanFixtures.Contains)));

        await Assert.That(exception.Message)
            .Contains($"Handler type '{Names.Of<ScanQueryHandler>()}' is already registered");
    }

    [Test]
    public async Task Scanning_the_same_assembly_twice_is_rejected()
    {
        ServiceCollection services = CreateServices();
        services.AddArbitrator(arbitrator => arbitrator
            .AddHandlersFromAssemblyContaining<ScanQuery>(ScanFixtures.Contains));

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddHandlersFromAssemblyContaining<ScanQuery>(ScanFixtures.Contains)));

        await Assert.That(exception.Message).Contains("is already registered");
    }

    [Test]
    public async Task Scan_after_a_sender_was_resolved_is_rejected()
    {
        ServiceCollection services = CreateServices();
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        _ = scope.ServiceProvider.GetRequiredService<ISender>();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddHandlersFromAssemblyContaining<ScanQuery>(ScanFixtures.Contains)));

        await Assert.That(exception.Message).Contains("cannot change once a service provider");
    }

    [Test]
    public async Task Scan_on_a_read_only_collection_is_rejected()
    {
        ServiceCollection services = CreateServices();
        services.MakeReadOnly();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddHandlersFromAssemblyContaining<ScanQuery>(ScanFixtures.Contains)));

        await Assert.That(exception.Message).Contains("read-only");
    }

    [Test]
    public async Task Null_assembly_and_null_filter_are_rejected()
    {
        var services = new ServiceCollection();

        ArgumentNullException nullAssembly = Expect.ThrowsExactly<ArgumentNullException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandlersFromAssembly(null!)));
        ArgumentNullException nullAssemblyWithFilter = Expect.ThrowsExactly<ArgumentNullException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddHandlersFromAssembly(null!, ScanFixtures.Contains)));
        ArgumentNullException nullFilter = Expect.ThrowsExactly<ArgumentNullException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddHandlersFromAssembly(typeof(ScanQuery).Assembly, null!)));
        ArgumentNullException nullMarkerFilter = Expect.ThrowsExactly<ArgumentNullException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandlersFromAssemblyContaining<ScanQuery>(null!)));

        await Assert.That(nullAssembly.ParamName).IsEqualTo("assembly");
        await Assert.That(nullAssemblyWithFilter.ParamName).IsEqualTo("assembly");
        await Assert.That(nullFilter.ParamName).IsEqualTo("filter");
        await Assert.That(nullMarkerFilter.ParamName).IsEqualTo("filter");
    }

    [Test]
    public async Task Scan_on_a_copied_collection_does_not_change_the_original()
    {
        ServiceCollection original = CreateServices();
        IServiceCollection copy = new ServiceCollection();
        foreach (ServiceDescriptor descriptor in original)
        {
            copy.Add(descriptor);
        }

        copy.AddArbitrator(arbitrator => arbitrator
            .AddHandlersFromAssemblyContaining<ScanQuery>(ScanFixtures.Contains));
        await using ServiceProvider originalProvider = original.BuildServiceProvider(TestHost.StrictValidation);
        await using ServiceProvider copyProvider = copy.BuildServiceProvider(TestHost.StrictValidation);
        await using AsyncServiceScope originalScope = originalProvider.CreateAsyncScope();
        await using AsyncServiceScope copyScope = copyProvider.CreateAsyncScope();

        string response = await copyScope.ServiceProvider.GetRequiredService<ISender>()
            .SendAsync(new ScanQuery("copy"));

        await Assert.That(response).IsEqualTo("copy");
        await Expect.ThrowsExactlyAsync<ArbitratorConfigurationException>(
            () => originalScope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new ScanQuery("original")));
    }

    [Test]
    public async Task Filter_that_registers_another_handler_does_not_deadlock_and_both_registrations_apply()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandlersFromAssemblyContaining<ScanQuery>(type => AcceptFixturesRegisteringPing(arbitrator, type)));

        Pong pong = await host.Sender.SendAsync(new Ping("ping"));
        string response = await host.Sender.SendAsync(new ScanQuery("query"));

        await Assert.That(pong.Message).IsEqualTo("ping");
        await Assert.That(response).IsEqualTo("query");
    }

    [Test]
    public async Task Filter_that_registers_a_discovered_type_makes_the_scan_fail_as_a_duplicate()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandlersFromAssemblyContaining<ScanQuery>(
                type =>
                {
                    if (type == typeof(ScanQueryHandler))
                    {
                        arbitrator.AddHandler<ScanQueryHandler>();
                    }

                    return ScanFixtures.Contains(type);
                })));

        await Assert.That(exception.Message)
            .Contains($"Handler type '{Names.Of<ScanQueryHandler>()}' is already registered");
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Journal>();
        services.AddArbitrator(arbitrator => arbitrator.AddHandler<PingHandler>());
        return services;
    }

    private static bool AcceptFixturesRegisteringPing(ArbitratorBuilder arbitrator, Type type)
    {
        if (type == typeof(AlphaNoticeHandler))
        {
            arbitrator.AddHandler<PingHandler>();
        }

        return ScanFixtures.Contains(type);
    }

    public sealed class InterfaceNotificationHandler : INotificationHandler<INotification>
    {
        public Task HandleAsync(INotification notification, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
