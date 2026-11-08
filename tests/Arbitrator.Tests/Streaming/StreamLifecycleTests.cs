using System.Reflection;
using System.Runtime.CompilerServices;
using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Streaming;

/// <summary>
/// Specifies when a stream does its work: nothing until it is read, a full dispatch for every read, and an orderly
/// end when the caller stops early.
/// </summary>
public sealed class StreamLifecycleTests
{
    [Test]
    public async Task Creating_a_stream_runs_nothing_until_it_is_read()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstStreamBehavior<,>))
            .AddHandler<CountdownHandler>());

        IAsyncEnumerable<Tick> stream = host.Sender.StreamAsync(new Countdown(1));
        IReadOnlyList<string> beforeReading = host.Journal.Entries;
        await Streams.ReadAllAsync(stream);

        await Assert.That(beforeReading).IsEmpty();
        await Assert.That(host.Journal.Entries).Contains("handler.end");
    }

    [Test]
    public async Task Null_request_fails_on_the_first_read_instead_of_when_the_stream_is_created()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<CountdownHandler>());

        // Deliberately violates the non-null request contract.
        IAsyncEnumerable<Tick> stream = host.Sender.StreamAsync<Tick>(null!);

        ArgumentNullException exception = await Expect.ThrowsExactlyAsync<ArgumentNullException>(
            () => Streams.ReadAllAsync(stream));

        await Assert.That(exception.ParamName).IsEqualTo("request");
    }

    [Test]
    public async Task Missing_stream_handler_fails_on_the_first_read_and_resolves_nothing()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstStreamBehavior<,>))
            .AddHandler<PingHandler>());

        IAsyncEnumerable<Tick> stream = host.Sender.StreamAsync(new Countdown(1));

        await Expect.ThrowsExactlyAsync<ArbitratorConfigurationException>(() => Streams.ReadAllAsync(stream));
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Each_read_of_one_stream_is_a_separate_dispatch_with_new_components()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<CountdownHandler>());
        IAsyncEnumerable<Tick> stream = host.Sender.StreamAsync(new Countdown(1));

        await Streams.ReadAllAsync(stream);
        await Streams.ReadAllAsync(stream);

        await Expect.EntriesAsync(host.Journal, "handler.yield 1", "handler.end", "handler.yield 1", "handler.end");
        await Assert.That(host.Journal.Captured<CountdownHandler>().Distinct().Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Concurrent_reads_of_one_stream_get_distinct_components()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<CountdownHandler>());
        IAsyncEnumerable<Tick> stream = host.Sender.StreamAsync(new Countdown(2));
        await using IAsyncEnumerator<Tick> first = stream.GetAsyncEnumerator();
        await using IAsyncEnumerator<Tick> second = stream.GetAsyncEnumerator();

        await first.MoveNextAsync();
        await second.MoveNextAsync();

        await Assert.That(first.Current).IsNotSameReferenceAs(second.Current);
        await Assert.That(host.Journal.Captured<CountdownHandler>().Distinct().Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Stopping_early_disposes_every_component_and_requests_no_further_item()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<FinallyBehavior>()
            .AddHandler<FinallyHandler>());

        await foreach (Tick _ in host.Sender.StreamAsync(new Countdown(3)))
        {
            break;
        }

        await Expect.EntriesAsync(host.Journal, "handler.yield 3", "handler.finally", "behavior.finally");
    }

    [Test]
    public async Task Reading_after_the_scope_is_disposed_fails_on_the_first_read()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<CountdownHandler>());
        IAsyncEnumerable<Tick> stream = host.Sender.StreamAsync(new Countdown(1));

        await host.Scope.DisposeAsync();

        await Expect.ThrowsExactlyAsync<ObjectDisposedException>(() => Streams.ReadAllAsync(stream));
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Activation_failure_propagates_unwrapped_from_the_first_read_before_any_component_runs()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstStreamBehavior<,>))
            .AddHandler<ExplodingStreamHandler>());

        ActivationFailedException thrown = await Expect.ThrowsExactlyAsync<ActivationFailedException>(
            () => Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1))));

        await Assert.That(thrown).IsSameReferenceAs(host.Journal.Captured<ActivationFailedException>().Single());
        await Assert.That(thrown).IsNotTypeOf<TargetInvocationException>();
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    public sealed class FinallyBehavior(Journal journal) : IStreamPipelineBehavior<Countdown, Tick>
    {
        public async IAsyncEnumerable<Tick> HandleAsync(
            Countdown request,
            StreamContinuation<Tick> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            try
            {
                await foreach (Tick tick in next(cancellationToken))
                {
                    yield return tick;
                }
            }
            finally
            {
                journal.Record("behavior.finally");
            }
        }
    }

    public sealed class FinallyHandler(Journal journal) : IStreamRequestHandler<Countdown, Tick>
    {
        public async IAsyncEnumerable<Tick> HandleAsync(
            Countdown request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            try
            {
                for (int value = request.From; value > 0; value--)
                {
                    journal.Record($"handler.yield {value}");
                    yield return new Tick(value);
                }
            }
            finally
            {
                journal.Record("handler.finally");
            }
        }
    }

    public sealed class ExplodingStreamHandler : IStreamRequestHandler<Countdown, Tick>
    {
        public ExplodingStreamHandler(Journal journal)
        {
            var failure = new ActivationFailedException();
            journal.Capture(failure);
            throw failure;
        }

        public IAsyncEnumerable<Tick> HandleAsync(Countdown request, CancellationToken cancellationToken)
        {
            return AsyncEnumerable.Empty<Tick>();
        }
    }

    public sealed class ActivationFailedException : Exception
    {
        public ActivationFailedException()
            : base("Activation failed.")
        {
        }
    }
}
