using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.ContainerValidation;

/// <summary>
/// Specifies the configuration errors found by container validation and by the first resolution of the sender.
/// </summary>
public sealed class ContainerValidationTests
{
    [Test]
    public async Task Container_validation_reports_a_handler_with_an_unresolvable_dependency()
    {
        var services = new ServiceCollection();
        services.AddArbitrator(arbitrator => arbitrator.AddHandler<HandlerWithMissingDependency>());

        AggregateException exception = Expect.ThrowsExactly<AggregateException>(
            () => services.BuildServiceProvider(TestHost.StrictValidation));

        await Assert.That(exception.Message).Contains(nameof(HandlerWithMissingDependency));
        await Assert.That(exception.Message).Contains(nameof(IMissingDependency));
    }

    [Test]
    public async Task Container_validation_reports_a_closed_behavior_with_an_unresolvable_dependency()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Journal>();
        services.AddArbitrator(arbitrator => arbitrator
            .AddBehavior<ClosedBehaviorWithMissingDependency>()
            .AddHandler<PingHandler>());

        AggregateException exception = Expect.ThrowsExactly<AggregateException>(
            () => services.BuildServiceProvider(TestHost.StrictValidation));

        await Assert.That(exception.Message).Contains(nameof(ClosedBehaviorWithMissingDependency));
    }

    [Test]
    public async Task Container_validation_reports_an_open_behavior_with_an_unresolvable_dependency()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Journal>();
        services.AddArbitrator(arbitrator => arbitrator
            .AddOpenBehavior(typeof(OpenBehaviorWithMissingDependency<,>))
            .AddHandler<PingHandler>());

        AggregateException exception = Expect.ThrowsExactly<AggregateException>(
            () => services.BuildServiceProvider(TestHost.StrictValidation));

        await Assert.That(exception.Message).Contains("OpenBehaviorWithMissingDependency");
    }

    [Test]
    public async Task Container_validation_reports_a_singleton_that_captures_the_sender()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SenderCapturingSingleton>();
        services.AddArbitrator(_ => { });

        AggregateException exception = Expect.ThrowsExactly<AggregateException>(
            () => services.BuildServiceProvider(TestHost.StrictValidation));

        await Assert.That(exception.Message).Contains("Cannot consume scoped service 'Arbitrator.ISender'");
    }

    [Test]
    public async Task Closed_behavior_without_a_handler_fails_every_sender_resolution()
    {
        var services = new ServiceCollection();
        services.AddArbitrator(arbitrator => arbitrator.AddBehavior<OrphanBehavior>());
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        ArbitratorConfigurationException first = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => scope.ServiceProvider.GetRequiredService<ISender>());
        ArbitratorConfigurationException second = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => scope.ServiceProvider.GetRequiredService<ISender>());

        await Assert.That(first.Message).Contains($"Behavior '{Names.Of<OrphanBehavior>()}' implements");
        await Assert.That(first.Message).Contains($"IPipelineBehavior<{Names.Of<Orphan>()}, Arbitrator.Unit>");
        await Assert.That(second.Message).IsEqualTo(first.Message);
    }

    public interface IMissingDependency;

    public sealed class HandlerWithMissingDependency(IMissingDependency dependency) : IRequestHandler<Ping, Pong>
    {
        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new Pong(dependency.ToString() ?? string.Empty));
        }
    }

    public sealed class ClosedBehaviorWithMissingDependency(IMissingDependency dependency)
        : IPipelineBehavior<Ping, Pong>
    {
        public Task<Pong> HandleAsync(Ping request, RequestContinuation<Pong> next, CancellationToken cancellationToken)
        {
            GC.KeepAlive(dependency);
            return next(cancellationToken);
        }
    }

    public sealed class OpenBehaviorWithMissingDependency<TRequest, TResponse>(IMissingDependency dependency)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request,
            RequestContinuation<TResponse> next,
            CancellationToken cancellationToken)
        {
            GC.KeepAlive(dependency);
            return next(cancellationToken);
        }
    }

    public sealed class SenderCapturingSingleton(ISender sender)
    {
        public ISender Sender { get; } = sender;
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
}
