using System.Runtime.CompilerServices;
using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Streaming;

/// <summary>
/// Specifies the rules a stream behavior must follow when it uses its continuation, and how null streams are
/// reported.
/// </summary>
public sealed class StreamGuardTests
{
    [Test]
    public async Task Second_continuation_call_is_rejected_and_the_handler_runs_once()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<RepeatingBehavior>()
            .AddHandler<CountdownHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1))));

        await Assert.That(exception.Message).Contains($"Behavior '{Names.Of<RepeatingBehavior>()}'");
        await Assert.That(exception.Message).Contains("invoked its continuation more than once");
        await Expect.EntriesAsync(host.Journal, "handler.yield 1", "handler.end");
    }

    [Test]
    public async Task Second_read_of_the_continuation_stream_is_rejected_and_the_handler_runs_once()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<RereadingBehavior>()
            .AddHandler<CountdownHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1))));

        await Assert.That(exception.Message).Contains($"Behavior '{Names.Of<RereadingBehavior>()}'");
        await Assert.That(exception.Message).Contains("read the stream returned by its continuation more than once");
        await Assert.That(exception.Message).Contains($"while handling '{Names.Of<Countdown>()}'");
        await Expect.EntriesAsync(host.Journal, "handler.yield 1", "handler.end");
    }

    [Test]
    public async Task Handler_returning_a_null_stream_is_reported_by_name()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<NullStreamHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1))));

        await Assert.That(exception.Message).Contains($"Handler '{Names.Of<NullStreamHandler>()}'");
        await Assert.That(exception.Message).Contains("null stream");
        await Assert.That(exception.Message).Contains($"while handling '{Names.Of<Countdown>()}'");
    }

    [Test]
    public async Task Handler_returning_a_null_stream_inside_a_pipeline_is_reported_by_name()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstStreamBehavior<,>))
            .AddHandler<NullStreamHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1))));

        await Assert.That(exception.Message).Contains($"Handler '{Names.Of<NullStreamHandler>()}'");
    }

    [Test]
    public async Task Outermost_behavior_returning_a_null_stream_is_reported_by_name()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<NullStreamBehavior>()
            .AddHandler<CountdownHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1))));

        await Assert.That(exception.Message).Contains($"Behavior '{Names.Of<NullStreamBehavior>()}'");
        await Assert.That(exception.Message).Contains("null stream");
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Inner_behavior_returning_a_null_stream_is_reported_by_name()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstStreamBehavior<,>))
            .AddBehavior<NullStreamBehavior>()
            .AddHandler<CountdownHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1))));

        await Assert.That(exception.Message).Contains($"Behavior '{Names.Of<NullStreamBehavior>()}'");
        await Expect.EntriesAsync(host.Journal, "StreamFirst.before");
    }

    public sealed class RepeatingBehavior : IStreamPipelineBehavior<Countdown, Tick>
    {
        public async IAsyncEnumerable<Tick> HandleAsync(
            Countdown request,
            StreamContinuation<Tick> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (Tick tick in next(cancellationToken))
            {
                yield return tick;
            }

            await foreach (Tick tick in next(cancellationToken))
            {
                yield return tick;
            }
        }
    }

    public sealed class RereadingBehavior : IStreamPipelineBehavior<Countdown, Tick>
    {
        public async IAsyncEnumerable<Tick> HandleAsync(
            Countdown request,
            StreamContinuation<Tick> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            IAsyncEnumerable<Tick> inner = next(cancellationToken);

            await foreach (Tick tick in inner)
            {
                yield return tick;
            }

            await foreach (Tick tick in inner)
            {
                yield return tick;
            }
        }
    }

    public sealed class NullStreamHandler : IStreamRequestHandler<Countdown, Tick>
    {
        // Deliberately violates the non-null stream contract.
        public IAsyncEnumerable<Tick> HandleAsync(Countdown request, CancellationToken cancellationToken) => null!;
    }

    public sealed class NullStreamBehavior : IStreamPipelineBehavior<Countdown, Tick>
    {
        // Deliberately violates the non-null stream contract.
        public IAsyncEnumerable<Tick> HandleAsync(
            Countdown request,
            StreamContinuation<Tick> next,
            CancellationToken cancellationToken) => null!;
    }
}
