using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Registration;

/// <summary>
/// Specifies behavior registration and its validation.
/// </summary>
public sealed class BehaviorRegistrationTests
{
    [Test]
    public async Task Open_behavior_registered_twice_is_rejected_with_its_existing_position()
    {
        var services = new ServiceCollection();
        services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(typeof(FirstBehavior<,>)));

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(typeof(FirstBehavior<,>))));

        await Assert.That(exception.Message)
            .Contains("'Arbitrator.Tests.Support.FirstBehavior<,>' is already registered at position 1");
    }

    [Test]
    public async Task Closed_behavior_registered_twice_is_rejected()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddBehavior<PingBehavior>()
                .AddBehavior<PingBehavior>()));

        await Assert.That(exception.Message).Contains($"'{Names.Of<PingBehavior>()}' is already registered");
    }

    [Test]
    public async Task Closed_generic_type_passed_as_open_behavior_points_to_its_definition()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(typeof(FirstBehavior<Ping, Pong>))));

        await Assert.That(exception.Message).Contains("is a closed type");
        await Assert.That(exception.Message).Contains("'Arbitrator.Tests.Support.FirstBehavior<,>'");
    }

    [Test]
    public async Task Non_generic_type_passed_as_open_behavior_is_rejected()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(typeof(PingBehavior))));

        await Assert.That(exception.Message).Contains("AddBehavior<TBehavior>()");
    }

    [Test]
    public async Task Generic_parameter_passed_as_open_behavior_is_rejected_as_a_type_parameter()
    {
        var services = new ServiceCollection();
        Type typeParameter = typeof(NotABehavior<,>).GetGenericArguments()[0];

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(typeParameter)));

        await Assert.That(exception.Message).Contains("'TRequest' is a generic type parameter");
    }

    [Test]
    public async Task Partially_closed_type_passed_as_open_behavior_points_to_its_definition()
    {
        var services = new ServiceCollection();
        Type responseParameter = typeof(NotABehavior<,>).GetGenericArguments()[1];
        Type partiallyClosed = typeof(NotABehavior<,>).MakeGenericType(typeof(Ping), responseParameter);

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(partiallyClosed)));

        await Assert.That(exception.Message).Contains("is partially closed");
        await Assert.That(exception.Message).Contains($"'{Names.Of<BehaviorRegistrationTests>()}.NotABehavior<,>'");
    }

    [Test]
    public async Task Open_behavior_that_is_a_value_type_is_rejected()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(typeof(ValueTypeBehavior<,>))));

        await Assert.That(exception.Message).Contains("is a value type; behaviors must be classes");
    }

    [Test]
    [Arguments(typeof(SingleParameterBehavior<>))]
    [Arguments(typeof(SwappedParametersBehavior<,>))]
    [Arguments(typeof(NotABehavior<,>))]
    public async Task Open_behavior_without_the_two_parameter_behavior_shape_is_rejected(Type openBehaviorType)
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(openBehaviorType)));

        await Assert.That(exception.Message).Contains("must be a generic type definition with exactly two type");
    }

    [Test]
    public async Task Abstract_behaviors_are_rejected()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException open = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(typeof(AbstractBehavior<,>))));
        ArbitratorConfigurationException closed = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddBehavior<AbstractBehavior<Ping, Pong>>()));

        await Assert.That(open.Message).Contains("is abstract or an interface");
        await Assert.That(closed.Message).Contains("is abstract or an interface");
    }

    [Test]
    public async Task Type_without_a_behavior_contract_is_rejected_as_a_closed_behavior()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddBehavior<PingHandler>()));

        await Assert.That(exception.Message).Contains(
            "implements neither IPipelineBehavior<TRequest, TResponse> nor IStreamPipelineBehavior<TRequest, TItem>");
    }

    [Test]
    public async Task Behavior_that_would_run_twice_is_rejected_when_its_handler_is_registered()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddOpenBehavior(typeof(FirstBehavior<,>))
                .AddBehavior<FirstBehavior<Ping, Pong>>()
                .AddHandler<PingHandler>()));

        await Assert.That(exception.Message).Contains("would run twice for request 'Arbitrator.Tests.Support.Ping'");
        await Assert.That(exception.Message).Contains("at position 1");
        await Assert.That(exception.Message).Contains("at position 2");
    }

    [Test]
    public async Task Behavior_that_would_run_twice_is_rejected_when_the_open_behavior_is_registered()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddBehavior<FirstBehavior<Ping, Pong>>()
                .AddHandler<PingHandler>()
                .AddOpenBehavior(typeof(FirstBehavior<,>))));

        await Assert.That(exception.Message).Contains("would run twice");
    }

    [Test]
    public async Task Null_open_behavior_type_is_rejected()
    {
        var services = new ServiceCollection();

        ArgumentNullException exception = Expect.ThrowsExactly<ArgumentNullException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(null!)));

        await Assert.That(exception.ParamName).IsEqualTo("openBehaviorType");
    }

    public sealed class PingBehavior : IPipelineBehavior<Ping, Pong>
    {
        public Task<Pong> HandleAsync(Ping request, RequestContinuation<Pong> next, CancellationToken cancellationToken)
        {
            return next(cancellationToken);
        }
    }

    public sealed class SingleParameterBehavior<TRequest> : IPipelineBehavior<TRequest, Unit>
        where TRequest : IRequest
    {
        public Task<Unit> HandleAsync(
            TRequest request,
            RequestContinuation<Unit> next,
            CancellationToken cancellationToken)
        {
            return next(cancellationToken);
        }
    }

    public sealed class SwappedParametersBehavior<TResponse, TRequest> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request,
            RequestContinuation<TResponse> next,
            CancellationToken cancellationToken)
        {
            return next(cancellationToken);
        }
    }

    public sealed class NotABehavior<TRequest, TResponse>;

    public struct ValueTypeBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public readonly Task<TResponse> HandleAsync(
            TRequest request,
            RequestContinuation<TResponse> next,
            CancellationToken cancellationToken)
        {
            return next(cancellationToken);
        }
    }

    public abstract class AbstractBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request,
            RequestContinuation<TResponse> next,
            CancellationToken cancellationToken)
        {
            return next(cancellationToken);
        }
    }
}
