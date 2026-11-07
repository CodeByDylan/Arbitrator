using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Registration;

/// <summary>
/// Specifies how registrations relate to the service providers and service collections they are made against.
/// </summary>
public sealed class RegistrationLifecycleTests
{
    [Test]
    public async Task Handler_registered_after_the_provider_was_built_is_reported_when_its_sender_is_resolved()
    {
        ServiceCollection services = CreateServices();
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        services.AddArbitrator(arbitrator => arbitrator.AddHandler<NotifyHandler>());
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => scope.ServiceProvider.GetRequiredService<ISender>());

        await Assert.That(exception.Message).Contains($"'{Names.Of<NotifyHandler>()}'");
        await Assert.That(exception.Message).Contains("registered after this service provider was built");
    }

    [Test]
    public async Task Behavior_registered_after_the_provider_was_built_is_reported_instead_of_breaking_requests()
    {
        ServiceCollection services = CreateServices();
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(typeof(FirstBehavior<,>)));
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => scope.ServiceProvider.GetRequiredService<ISender>());

        string closedBehavior = $"Arbitrator.Tests.Support.FirstBehavior<{Names.Of<Ping>()}, {Names.Of<Pong>()}>";
        await Assert.That(exception.Message).Contains($"'{closedBehavior}'");
    }

    [Test]
    public async Task Providers_built_from_one_collection_dispatch_alike()
    {
        ServiceCollection services = CreateServices();
        await using ServiceProvider first = services.BuildServiceProvider(TestHost.StrictValidation);
        await using ServiceProvider second = services.BuildServiceProvider(TestHost.StrictValidation);

        Pong fromFirst = await SendPingAsync(first);
        Pong fromSecond = await SendPingAsync(second);

        await Assert.That(fromFirst.Message).IsEqualTo("hello");
        await Assert.That(fromSecond.Message).IsEqualTo("hello");
    }

    [Test]
    public async Task Registration_on_a_read_only_collection_is_rejected_without_changing_registrations()
    {
        ServiceCollection services = CreateServices();
        services.MakeReadOnly();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<NotifyHandler>()));
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await Assert.That(exception.Message).Contains("read-only");
        await Expect.ThrowsExactlyAsync<ArbitratorConfigurationException>(() => sender.SendAsync(new Notify("hi")));
        await Assert.That((await sender.SendAsync(new Ping("hello"))).Message).IsEqualTo("hello");
    }

    [Test]
    public async Task Registrations_on_a_copied_collection_do_not_change_the_original()
    {
        ServiceCollection original = CreateServices();
        IServiceCollection copy = new ServiceCollection();
        foreach (ServiceDescriptor descriptor in original)
        {
            copy.Add(descriptor);
        }

        copy.AddArbitrator(arbitrator => arbitrator.AddHandler<NotifyHandler>());
        await using ServiceProvider originalProvider = original.BuildServiceProvider(TestHost.StrictValidation);
        await using ServiceProvider copyProvider = copy.BuildServiceProvider(TestHost.StrictValidation);
        await using AsyncServiceScope originalScope = originalProvider.CreateAsyncScope();
        await using AsyncServiceScope copyScope = copyProvider.CreateAsyncScope();

        await Expect.ThrowsExactlyAsync<ArbitratorConfigurationException>(
            () => originalScope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new Notify("hi")));
        await copyScope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new Notify("hi"));
        await Assert.That((await SendPingAsync(copyProvider)).Message).IsEqualTo("hello");
    }

    [Test]
    public async Task Registration_after_a_failed_sender_resolution_is_rejected_with_an_accurate_message()
    {
        var services = new ServiceCollection();
        services.AddArbitrator(arbitrator => arbitrator.AddBehavior<OrphanBehavior>());
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            Expect.ThrowsExactly<ArbitratorConfigurationException>(
                () => scope.ServiceProvider.GetRequiredService<ISender>());
        }

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<OrphanHandler>()));

        await Assert.That(exception.Message).Contains("has attempted to resolve ISender");
    }

    [Test]
    public async Task Rejected_behavior_registration_leaves_the_collection_and_the_pipelines_unchanged()
    {
        int descriptorsBefore = 0;
        int descriptorsAfter = 0;

        await using TestHost host = TestHost.CreateWithServices(services =>
        {
            services.AddArbitrator(arbitrator => arbitrator
                .AddBehavior<FirstBehavior<Ping, Pong>>()
                .AddHandler<PingHandler>());
            descriptorsBefore = services.Count;
            Expect.ThrowsExactly<ArbitratorConfigurationException>(
                () => services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(typeof(FirstBehavior<,>))));
            descriptorsAfter = services.Count;
            services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(typeof(SecondBehavior<,>)));
        });

        await host.Sender.SendAsync(new Ping("hello"));

        await Assert.That(descriptorsAfter).IsEqualTo(descriptorsBefore);
        await Expect.EntriesAsync(
            host.Journal,
            "First.before",
            "Second.before",
            "handler",
            "Second.after",
            "First.after");
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Journal>();
        services.AddArbitrator(arbitrator => arbitrator.AddHandler<PingHandler>());
        return services;
    }

    private static async Task<Pong> SendPingAsync(ServiceProvider provider)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new Ping("hello"));
    }

    public sealed record Orphan : IRequest;

    public sealed class OrphanBehavior : IPipelineBehavior<Orphan, Unit>
    {
        public Task<Unit> HandleAsync(
            Orphan request,
            RequestContinuation<Unit> next,
            CancellationToken cancellationToken)
        {
            return next(cancellationToken);
        }
    }

    public sealed class OrphanHandler : IRequestHandler<Orphan>
    {
        public Task HandleAsync(Orphan request, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
