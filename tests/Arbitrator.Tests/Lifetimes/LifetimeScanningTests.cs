using Arbitrator.Tests.Scanning;
using Arbitrator.Tests.Scanning.Fixtures;
using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Lifetimes;

/// <summary>
/// Specifies that a scan registers every handler it accepts with the lifetime it is given, and as transient without
/// one.
/// </summary>
public sealed class LifetimeScanningTests
{
    [Test]
    public async Task Scan_without_a_lifetime_registers_transient_handlers()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandlersFromAssembly(CreateAssembly()));

        await Assert.That(HandlerLifetimes(host.Registrations).Distinct()).IsEquivalentTo([ServiceLifetime.Transient]);
    }

    [Test]
    [Arguments(ServiceLifetime.Scoped)]
    [Arguments(ServiceLifetime.Singleton)]
    public async Task Scan_registers_request_stream_and_notification_handlers_with_its_lifetime(
        ServiceLifetime lifetime)
    {
        await using TestHost host = TestHost.Create(
            arbitrator => arbitrator.AddHandlersFromAssembly(CreateAssembly(), lifetime));

        IReadOnlyList<ServiceLifetime> lifetimes = HandlerLifetimes(host.Registrations);

        await Assert.That(lifetimes.Count).IsEqualTo(3);
        await Assert.That(lifetimes.Distinct()).IsEquivalentTo([lifetime]);
    }

    [Test]
    public async Task Filtered_scan_registers_the_accepted_handlers_with_its_lifetime()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandlersFromAssembly(
            CreateAssembly(),
            type => type != typeof(CountdownHandler),
            ServiceLifetime.Singleton));

        ArbitratorRegistrations registrations = host.Registrations;

        await Assert.That(registrations.Requests.Single().HandlerType).IsEqualTo(typeof(PingHandler));
        await Assert.That(HandlerLifetimes(registrations).Distinct()).IsEquivalentTo([ServiceLifetime.Singleton]);
    }

    [Test]
    public async Task Filtered_marker_scan_registers_the_accepted_handlers_with_its_lifetime()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandlersFromAssemblyContaining<ScanQuery>(ScanFixtures.Contains, ServiceLifetime.Scoped));

        IReadOnlyList<ServiceLifetime> lifetimes = HandlerLifetimes(host.Registrations);

        await Assert.That(lifetimes).IsNotEmpty();
        await Assert.That(lifetimes.Distinct()).IsEquivalentTo([ServiceLifetime.Scoped]);
    }

    private static StubAssembly CreateAssembly()
    {
        return StubAssembly.Defining(
            "Contoso.Lifetimes",
            typeof(PingHandler),
            typeof(CountdownHandler),
            typeof(FirstAnnouncementHandler));
    }

    private static IReadOnlyList<ServiceLifetime> HandlerLifetimes(ArbitratorRegistrations registrations)
    {
        return
        [
            .. registrations.Requests.Select(request => request.Handler.Lifetime),
            .. registrations.Notifications.SelectMany(notification => notification.Handlers)
                .Select(handler => handler.Lifetime),
        ];
    }
}
