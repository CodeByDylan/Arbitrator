using System.Runtime.CompilerServices;
using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Streaming;

/// <summary>
/// Specifies how stream behaviors wrap the read of a stream, and how they stay apart from request behaviors.
/// </summary>
public sealed class StreamPipelineTests
{
    [Test]
    public async Task Stream_behaviors_wrap_the_whole_read_in_registration_order()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstStreamBehavior<,>))
            .AddOpenBehavior(typeof(SecondStreamBehavior<,>))
            .AddHandler<CountdownHandler>());

        await Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1)));

        await Expect.EntriesAsync(
            host.Journal,
            "StreamFirst.before",
            "StreamSecond.before",
            "handler.yield 1",
            "StreamSecond.item",
            "StreamFirst.item",
            "handler.end",
            "StreamSecond.after",
            "StreamFirst.after");
    }

    [Test]
    public async Task Request_and_stream_behaviors_share_one_order_but_never_wrap_each_others_handlers()
    {
        await using TestHost host = TestHost.CreateWithServices(services => services
            .AddArbitrator(arbitrator => arbitrator
                .AddOpenBehavior(typeof(FirstStreamBehavior<,>))
                .AddOpenBehavior(typeof(FirstBehavior<,>)))
            .AddArbitrator(arbitrator => arbitrator
                .AddHandler<PingHandler>()
                .AddOpenBehavior(typeof(SecondStreamBehavior<,>))
                .AddHandler<CountdownHandler>()));

        await host.Sender.SendAsync(new Ping("hello"));
        await Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(0)));

        await Expect.EntriesAsync(
            host.Journal,
            "First.before",
            "handler",
            "First.after",
            "StreamFirst.before",
            "StreamSecond.before",
            "handler.end",
            "StreamSecond.after",
            "StreamFirst.after");
    }

    [Test]
    public async Task Constrained_open_stream_behavior_applies_only_where_its_constraints_hold()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(ValueItemBehavior<,>))
            .AddHandler<CountdownHandler>()
            .AddHandler<NumbersHandler>());

        await Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1)));
        await Streams.ReadAllAsync(host.Sender.StreamAsync(new Numbers(1)));

        await Expect.EntriesAsync(host.Journal, "handler.yield 1", "handler.end", "value items");
    }

    [Test]
    public async Task Short_circuiting_stream_behavior_produces_the_items_and_the_handler_never_runs()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<CachedCountdownBehavior>()
            .AddHandler<CountdownHandler>());

        List<Tick> items = await Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(3)));

        await Assert.That(items.Single()).IsSameReferenceAs(CachedCountdownBehavior.CachedTick);
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Stream_behavior_may_filter_and_transform_items()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<EvenTicksDoubledBehavior>()
            .AddHandler<CountdownHandler>());

        List<Tick> items = await Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(4)));

        await Assert.That(items.Select(item => item.Value)).IsEquivalentTo([8, 4]);
    }

    [Test]
    public async Task Closed_behavior_implementing_both_kinds_wraps_both_pipelines()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<DualBehavior>()
            .AddHandler<PingHandler>()
            .AddHandler<CountdownHandler>());

        await host.Sender.SendAsync(new Ping("hello"));
        await Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(0)));

        await Expect.EntriesAsync(host.Journal, "dual request", "handler", "dual stream", "handler.end");
    }

    [Test]
    public async Task Closed_stream_behavior_that_an_open_one_already_produces_is_rejected()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator
                .AddOpenBehavior(typeof(FirstStreamBehavior<,>))
                .AddHandler<CountdownHandler>()
                .AddBehavior<FirstStreamBehavior<Countdown, Tick>>()));

        await Assert.That(exception.Message).Contains($"would run twice for request '{Names.Of<Countdown>()}'");
        await Assert.That(exception.Message).Contains("at position 1");
        await Assert.That(exception.Message).Contains("at position 2");
    }

    public sealed record Numbers(int Count) : IStreamRequest<int>;

    public sealed class NumbersHandler : IStreamRequestHandler<Numbers, int>
    {
        public async IAsyncEnumerable<int> HandleAsync(
            Numbers request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int value = 1; value <= request.Count; value++)
            {
                yield return value;
            }
        }
    }

    public sealed class ValueItemBehavior<TRequest, TItem>(Journal journal) : IStreamPipelineBehavior<TRequest, TItem>
        where TRequest : IStreamRequest<TItem>
        where TItem : struct
    {
        public IAsyncEnumerable<TItem> HandleAsync(
            TRequest request,
            StreamContinuation<TItem> next,
            CancellationToken cancellationToken)
        {
            journal.Record("value items");
            return next(cancellationToken);
        }
    }

    public sealed class CachedCountdownBehavior : IStreamPipelineBehavior<Countdown, Tick>
    {
        public static Tick CachedTick { get; } = new(42);

        public async IAsyncEnumerable<Tick> HandleAsync(
            Countdown request,
            StreamContinuation<Tick> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return CachedTick;
        }
    }

    public sealed class EvenTicksDoubledBehavior : IStreamPipelineBehavior<Countdown, Tick>
    {
        public async IAsyncEnumerable<Tick> HandleAsync(
            Countdown request,
            StreamContinuation<Tick> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (Tick tick in next(cancellationToken))
            {
                if (tick.Value % 2 == 0)
                {
                    yield return new Tick(tick.Value * 2);
                }
            }
        }
    }

    public sealed class DualBehavior(Journal journal)
        : IPipelineBehavior<Ping, Pong>, IStreamPipelineBehavior<Countdown, Tick>
    {
        public Task<Pong> HandleAsync(Ping request, RequestContinuation<Pong> next, CancellationToken cancellationToken)
        {
            journal.Record("dual request");
            return next(cancellationToken);
        }

        public IAsyncEnumerable<Tick> HandleAsync(
            Countdown request,
            StreamContinuation<Tick> next,
            CancellationToken cancellationToken)
        {
            journal.Record("dual stream");
            return next(cancellationToken);
        }
    }
}
