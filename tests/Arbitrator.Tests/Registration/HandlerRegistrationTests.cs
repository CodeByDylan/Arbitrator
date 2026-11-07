using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Registration;

/// <summary>
/// Specifies handler registration, its validation, and repeated registration calls.
/// </summary>
public sealed class HandlerRegistrationTests
{
    [Test]
    public async Task Second_handler_for_a_request_type_is_rejected_and_both_handlers_are_named()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddHandler<PingHandler>()
                .AddHandler<OtherPingHandler>()));

        await Assert.That(exception.Message).Contains(Names.Of<PingHandler>());
        await Assert.That(exception.Message).Contains(Names.Of<OtherPingHandler>());
        await Assert.That(exception.Message).Contains("exactly one handler");
    }

    [Test]
    public async Task Same_handler_type_registered_twice_is_rejected()
    {
        var services = new ServiceCollection();
        services.AddArbitrator(arbitrator => arbitrator.AddHandler<PingHandler>());

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<PingHandler>()));

        await Assert.That(exception.Message)
            .Contains($"Handler type '{Names.Of<PingHandler>()}' is already registered");
    }

    [Test]
    public async Task Abstract_handler_and_interface_handler_are_rejected()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException abstractHandler = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<AbstractPingHandler>()));
        ArbitratorConfigurationException interfaceHandler = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<IRequestHandler<Ping, Pong>>()));

        await Assert.That(abstractHandler.Message).Contains("is abstract or an interface");
        await Assert.That(interfaceHandler.Message).Contains("is abstract or an interface");
    }

    [Test]
    public async Task Type_without_a_handler_contract_is_rejected()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<NotAHandler>()));

        await Assert.That(exception.Message).Contains($"Type '{Names.Of<NotAHandler>()}' cannot be registered");
    }

    [Test]
    public async Task Handler_for_an_abstract_request_type_is_rejected()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<AbstractQueryHandler>()));

        await Assert.That(exception.Message).Contains(Names.Of<AbstractQuery>());
        await Assert.That(exception.Message).Contains("could never run");
    }

    [Test]
    public async Task Handler_for_a_request_type_with_two_request_contracts_is_rejected()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<AmbiguousRequestHandler>()));

        await Assert.That(exception.Message).Contains("Arbitrator.IRequest<int>");
        await Assert.That(exception.Message).Contains("Arbitrator.IRequest<string>");
    }

    [Test]
    public async Task Rejected_registration_leaves_the_service_collection_and_the_registrations_unchanged()
    {
        int descriptorsBefore = 0;
        int descriptorsAfter = 0;

        await using TestHost host = TestHost.CreateWithServices(services =>
        {
            services.AddArbitrator(arbitrator => arbitrator.AddHandler<PingHandler>());
            descriptorsBefore = services.Count;
            Expect.ThrowsExactly<ArbitratorConfigurationException>(
                () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<PingAndBrokenHandlers>()));
            descriptorsAfter = services.Count;
        });

        await Assert.That(descriptorsAfter).IsEqualTo(descriptorsBefore);
        await Assert.ThrowsExactlyAsync<ArbitratorConfigurationException>(() => host.Sender.SendAsync(new Broken()));
        await host.Sender.SendAsync(new Ping("hello"));
        await Expect.EntriesAsync(host.Journal, "handler");
    }

    [Test]
    public async Task Repeated_registration_calls_add_one_sender_and_combine_their_registrations()
    {
        int senderDescriptorCount = 0;

        await using TestHost host = TestHost.CreateWithServices(services =>
        {
            services.AddArbitrator(arbitrator => arbitrator.AddHandler<PingHandler>());
            services.AddArbitrator(arbitrator => arbitrator.AddHandler<NotifyHandler>());
            senderDescriptorCount = services.Count(descriptor => descriptor.ServiceType == typeof(ISender));
        });

        await host.Sender.SendAsync(new Ping("hello"));
        await host.Sender.SendAsync(new Notify("hello"));

        await Assert.That(senderDescriptorCount).IsEqualTo(1);
        await Expect.EntriesAsync(host.Journal, "handler", "handler");
    }

    [Test]
    public async Task Registration_after_a_provider_resolved_the_sender_is_rejected()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Journal>();
        services.AddArbitrator(arbitrator => arbitrator.AddHandler<PingHandler>());
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ISender>();
        }

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<NotifyHandler>()));

        await Assert.That(exception.Message).Contains("has attempted to resolve ISender");
    }

    [Test]
    public async Task Application_registration_of_a_handler_type_does_not_change_its_lifetime_in_dispatch()
    {
        await using TestHost host = TestHost.CreateWithServices(services => services
            .AddSingleton<PingHandler>()
            .AddArbitrator(arbitrator => arbitrator.AddHandler<PingHandler>()));

        await host.Sender.SendAsync(new Ping("one"));
        await host.Sender.SendAsync(new Ping("two"));

        IReadOnlyList<PingHandler> instances = host.Journal.Captured<PingHandler>();
        await Assert.That(instances[0]).IsNotSameReferenceAs(instances[1]);
    }

    [Test]
    public async Task Null_configuration_callback_is_rejected()
    {
        var services = new ServiceCollection();

        ArgumentNullException exception = Expect.ThrowsExactly<ArgumentNullException>(
            () => services.AddArbitrator(null!));

        await Assert.That(exception.ParamName).IsEqualTo("configure");
    }

    public sealed class OtherPingHandler : IRequestHandler<Ping, Pong>
    {
        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new Pong("other"));
        }
    }

    public abstract class AbstractPingHandler : IRequestHandler<Ping, Pong>
    {
        public abstract Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken);
    }

    public sealed class NotAHandler;

    public abstract record AbstractQuery : IRequest<string>;

    public sealed class AbstractQueryHandler : IRequestHandler<AbstractQuery, string>
    {
        public Task<string> HandleAsync(AbstractQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult("never");
        }
    }

    public sealed record AmbiguousRequest : IRequest<int>, IRequest<string>;

    public sealed class AmbiguousRequestHandler : IRequestHandler<AmbiguousRequest, int>
    {
        public Task<int> HandleAsync(AmbiguousRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }

    public sealed record Broken : IRequest;

    public sealed class PingAndBrokenHandlers : IRequestHandler<Broken>, IRequestHandler<Ping, Pong>
    {
        public Task HandleAsync(Broken request, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new Pong("duplicate"));
        }
    }
}
