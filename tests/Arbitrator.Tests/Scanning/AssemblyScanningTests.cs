using System.Runtime.CompilerServices;
using Arbitrator.Tests.Scanning.Fixtures;
using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;

namespace Arbitrator.Tests.Scanning;

/// <summary>
/// Specifies which handler types an assembly scan registers, in what order, and how it combines with other
/// registrations.
/// </summary>
public sealed class AssemblyScanningTests
{
    [Test]
    public async Task Discovered_request_handlers_handle_their_requests_whatever_their_visibility()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandlersFromAssemblyContaining<ScanQuery>(ScanFixtures.Contains));

        string response = await host.Sender.SendAsync(new ScanQuery("hello"));
        await host.Sender.SendAsync(new ScanCommand());

        await Assert.That(response).IsEqualTo("hello");
        await Expect.EntriesAsync(host.Journal, "query", "command");
    }

    [Test]
    public async Task Discovered_notification_handlers_run_in_name_order_between_earlier_and_later_registrations()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<EarlyNoticeHandler>()
            .AddHandlersFromAssemblyContaining<ScanQuery>(ScanFixtures.Contains)
            .AddNotificationHandler<LateNoticeHandler>());

        await host.Publisher.PublishAsync(new ScanNotice());

        await Expect.EntriesAsync(host.Journal, "early", "alpha", "beta", "auditor notice", "late");
    }

    [Test]
    public async Task Type_with_request_and_notification_contracts_is_registered_for_both()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandlersFromAssembly(typeof(ScanQuery).Assembly, type => type == typeof(ScanAuditor)));

        await host.Sender.SendAsync(new ScanAudit());
        await host.Publisher.PublishAsync(new ScanNotice());

        await Expect.EntriesAsync(host.Journal, "auditor request", "auditor notice");
    }

    [Test]
    public async Task Behaviors_are_not_discovered_but_earlier_open_behaviors_wrap_discovered_handlers()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>))
            .AddHandlersFromAssemblyContaining<ScanQuery>(ScanFixtures.Contains));

        await host.Sender.SendAsync(new ScanQuery("hello"));

        await Expect.EntriesAsync(host.Journal, "First.before", "query", "First.after");
    }

    [Test]
    public async Task Abstract_interface_struct_static_generic_compiler_generated_and_behavior_types_are_skipped()
    {
        StubAssembly assembly = StubAssembly.Defining(
            "Contoso.Ineligible",
            typeof(RecordingNoticeHandler),
            typeof(IScanQueryHandler),
            typeof(NoticeStructHandler),
            typeof(ScanCommandHandlers),
            typeof(GenericScanHandler<>),
            typeof(GeneratedPingHandler),
            typeof(ScanQueryBehavior),
            typeof(ScanQuery));
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandlersFromAssembly(assembly)));

        await Assert.That(exception.Message).Contains("'Contoso.Ineligible' contains no request handler");
    }

    [Test]
    public async Task Attributes_of_classes_that_are_not_handlers_are_never_read()
    {
        var unreadableAttributes = new AttributeLoadFailingType(
            typeof(ScanNotice),
            new FileNotFoundException("Could not load file or assembly 'Contoso.Annotations'."));
        StubAssembly assembly = StubAssembly.Defining(
            "Contoso.Annotated",
            unreadableAttributes,
            typeof(AlphaNoticeHandler));

        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandlersFromAssembly(assembly));
        await host.Publisher.PublishAsync(new ScanNotice());

        await Expect.EntriesAsync(host.Journal, "alpha");
    }

    [Test]
    public async Task Handler_types_are_offered_to_the_filter_and_registered_in_full_name_order()
    {
        StubAssembly assembly = StubAssembly.Defining(
            "Contoso.Events",
            typeof(ScanAuditor),
            typeof(ScanNotice),
            typeof(BetaNoticeHandler),
            typeof(AlphaNoticeHandler));
        List<Type> offered = [];

        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandlersFromAssembly(
            assembly,
            type =>
            {
                offered.Add(type);
                return true;
            }));
        await host.Publisher.PublishAsync(new ScanNotice());

        Type[] expectedOrder = [typeof(AlphaNoticeHandler), typeof(BetaNoticeHandler), typeof(ScanAuditor)];
        await Assert.That(offered).IsEquivalentTo(expectedOrder, CollectionOrdering.Matching);
        await Expect.EntriesAsync(host.Journal, "alpha", "beta", "auditor notice");
    }

    [Test]
    public async Task Handler_types_the_filter_rejects_are_not_registered()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandlersFromAssemblyContaining<ScanQuery>(
                type => ScanFixtures.Contains(type) && type != typeof(BetaNoticeHandler)));

        await host.Publisher.PublishAsync(new ScanNotice());

        await Expect.EntriesAsync(host.Journal, "alpha", "auditor notice");
    }

    public sealed class EarlyNoticeHandler(Journal journal) : INotificationHandler<ScanNotice>
    {
        public Task HandleAsync(ScanNotice notification, CancellationToken cancellationToken)
        {
            journal.Record("early");
            return Task.CompletedTask;
        }
    }

    public sealed class LateNoticeHandler(Journal journal) : INotificationHandler<ScanNotice>
    {
        public Task HandleAsync(ScanNotice notification, CancellationToken cancellationToken)
        {
            journal.Record("late");
            return Task.CompletedTask;
        }
    }

    [CompilerGenerated]
    public sealed class GeneratedPingHandler : IRequestHandler<Ping, Pong>
    {
        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new Pong(request.Message));
        }
    }
}
