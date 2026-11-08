using System.Runtime.CompilerServices;
using Arbitrator.Tests.Scanning;
using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Streaming;

/// <summary>
/// Specifies how stream handlers and stream behaviors are registered through the existing builder methods and
/// assembly scanning, under the existing rules.
/// </summary>
public sealed class StreamRegistrationTests
{
    [Test]
    public async Task Request_type_with_a_request_and_a_stream_contract_is_rejected()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<AmbiguousHandler>()));

        await Assert.That(exception.Message).Contains($"Request type '{Names.Of<Ambiguous>()}'");
        await Assert.That(exception.Message).Contains("more than one request contract");
        await Assert.That(exception.Message).Contains("IStreamRequest<TItem>");
    }

    [Test]
    public async Task Request_type_with_two_stream_contracts_is_rejected()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<TwoStreamsHandler>()));

        await Assert.That(exception.Message).Contains($"Request type '{Names.Of<TwoStreams>()}'");
        await Assert.That(exception.Message).Contains("more than one request contract");
    }

    [Test]
    public async Task Second_handler_for_a_stream_request_type_is_rejected()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddHandler<CountdownHandler>()
                .AddHandler<OtherCountdownHandler>()));

        await Assert.That(exception.Message).Contains($"already has handler '{Names.Of<CountdownHandler>()}'");
    }

    [Test]
    public async Task Handler_for_an_abstract_stream_request_type_is_rejected()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<AbstractQueryHandler>()));

        await Assert.That(exception.Message).Contains($"handles '{Names.Of<AbstractQuery>()}'");
    }

    [Test]
    public async Task Open_behavior_implementing_both_pipeline_contracts_is_rejected()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(typeof(BothKindsBehavior<,>))));

        await Assert.That(exception.Message)
            .Contains("exactly one behavior contract using its type parameters in order");
    }

    [Test]
    public async Task Stream_and_request_behaviors_share_registration_positions()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddOpenBehavior(typeof(FirstBehavior<,>))
                .AddOpenBehavior(typeof(FirstStreamBehavior<,>))
                .AddOpenBehavior(typeof(FirstStreamBehavior<,>))));

        await Assert.That(exception.Message).Contains("already registered at position 2");
    }

    [Test]
    public async Task Closed_stream_behavior_without_a_handler_fails_the_first_sender_resolution()
    {
        var services = new ServiceCollection();
        services.AddArbitrator(arbitrator => arbitrator
            .AddBehavior<CountdownBehavior>()
            .AddHandler<PingHandler>());
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => scope.ServiceProvider.GetRequiredService<ISender>());

        await Assert.That(exception.Message)
            .Contains($"implements IStreamPipelineBehavior<{Names.Of<Countdown>()}, {Names.Of<Tick>()}>");
    }

    [Test]
    public async Task Stream_handler_registered_after_freezing_is_rejected()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Journal>();
        services.AddArbitrator(arbitrator => arbitrator.AddHandler<PingHandler>());
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ISender>();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<CountdownHandler>()));

        await Assert.That(exception.Message).Contains("cannot change once");
    }

    [Test]
    public async Task Scan_registers_stream_handlers_and_never_stream_behaviors()
    {
        StubAssembly assembly = StubAssembly.Defining(
            "Contoso.Streams",
            typeof(CountdownBehavior),
            typeof(CountdownHandler));

        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandlersFromAssembly(assembly));
        List<Tick> items = await Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1)));

        await Assert.That(items.Single().Value).IsEqualTo(1);
        await Expect.EntriesAsync(host.Journal, "handler.yield 1", "handler.end");
    }

    [Test]
    public async Task Container_validation_reports_stream_components_with_unresolvable_dependencies()
    {
        var services = new ServiceCollection();
        services.AddArbitrator(arbitrator => arbitrator
            .AddOpenBehavior(typeof(StreamBehaviorWithMissingDependency<,>))
            .AddHandler<StreamHandlerWithMissingDependency>());

        AggregateException exception = Expect.ThrowsExactly<AggregateException>(
            () => services.BuildServiceProvider(TestHost.StrictValidation));

        await Assert.That(exception.Message).Contains(nameof(StreamHandlerWithMissingDependency));
        await Assert.That(exception.Message).Contains("StreamBehaviorWithMissingDependency");
    }

    public sealed record Ambiguous : IRequest<int>, IStreamRequest<int>;

    public sealed class AmbiguousHandler : IStreamRequestHandler<Ambiguous, int>
    {
        public IAsyncEnumerable<int> HandleAsync(Ambiguous request, CancellationToken cancellationToken) =>
            AsyncEnumerable.Empty<int>();
    }

    public sealed record TwoStreams : IStreamRequest<int>, IStreamRequest<string>;

    public sealed class TwoStreamsHandler : IStreamRequestHandler<TwoStreams, int>
    {
        public IAsyncEnumerable<int> HandleAsync(TwoStreams request, CancellationToken cancellationToken) =>
            AsyncEnumerable.Empty<int>();
    }

    public sealed class OtherCountdownHandler : IStreamRequestHandler<Countdown, Tick>
    {
        public IAsyncEnumerable<Tick> HandleAsync(Countdown request, CancellationToken cancellationToken) =>
            AsyncEnumerable.Empty<Tick>();
    }

    public abstract record AbstractQuery : IStreamRequest<int>;

    public sealed class AbstractQueryHandler : IStreamRequestHandler<AbstractQuery, int>
    {
        public IAsyncEnumerable<int> HandleAsync(AbstractQuery request, CancellationToken cancellationToken) =>
            AsyncEnumerable.Empty<int>();
    }

    public sealed class BothKindsBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>, IStreamPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>, IStreamRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request,
            RequestContinuation<TResponse> next,
            CancellationToken cancellationToken) => next(cancellationToken);

        public IAsyncEnumerable<TResponse> HandleAsync(
            TRequest request,
            StreamContinuation<TResponse> next,
            CancellationToken cancellationToken) => next(cancellationToken);
    }

    public sealed class CountdownBehavior(Journal journal) : IStreamPipelineBehavior<Countdown, Tick>
    {
        public async IAsyncEnumerable<Tick> HandleAsync(
            Countdown request,
            StreamContinuation<Tick> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            journal.Record("countdown behavior");

            await foreach (Tick tick in next(cancellationToken))
            {
                yield return tick;
            }
        }
    }

    public interface IMissingDependency;

    public sealed class StreamHandlerWithMissingDependency(IMissingDependency dependency)
        : IStreamRequestHandler<Countdown, Tick>
    {
        public IAsyncEnumerable<Tick> HandleAsync(Countdown request, CancellationToken cancellationToken)
        {
            GC.KeepAlive(dependency);
            return AsyncEnumerable.Empty<Tick>();
        }
    }

    public sealed class StreamBehaviorWithMissingDependency<TRequest, TItem>(IMissingDependency dependency)
        : IStreamPipelineBehavior<TRequest, TItem>
        where TRequest : IStreamRequest<TItem>
    {
        public IAsyncEnumerable<TItem> HandleAsync(
            TRequest request,
            StreamContinuation<TItem> next,
            CancellationToken cancellationToken)
        {
            GC.KeepAlive(dependency);
            return next(cancellationToken);
        }
    }
}
