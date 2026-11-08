using System.Runtime.CompilerServices;
using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Streaming;

/// <summary>
/// Specifies how a stream combines the token passed to <see cref="ISender.StreamAsync{TItem}"/> with the token of a
/// read, checks it once, and otherwise only forwards it.
/// </summary>
public sealed class StreamCancellationTests
{
    [Test]
    public async Task Canceled_stream_token_cancels_the_read_before_anything_is_resolved()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstStreamBehavior<,>))
            .AddHandler<CountdownHandler>());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        IAsyncEnumerable<Tick> stream = host.Sender.StreamAsync(new Countdown(1), cancellation.Token);
        OperationCanceledException exception = await Expect.ThrowsExactlyAsync<OperationCanceledException>(
            () => Streams.ReadAllAsync(stream));

        await Assert.That(exception.CancellationToken).IsEqualTo(cancellation.Token);
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Canceled_read_token_cancels_the_read_before_anything_is_resolved()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<CountdownHandler>());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        OperationCanceledException exception = await Expect.ThrowsExactlyAsync<OperationCanceledException>(
            () => Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1)), cancellation.Token));

        await Assert.That(exception.CancellationToken).IsEqualTo(cancellation.Token);
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Missing_handler_is_reported_before_the_token_is_checked()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<PingHandler>());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Expect.ThrowsExactlyAsync<ArbitratorConfigurationException>(
            () => Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1), cancellation.Token)));
    }

    [Test]
    public async Task Components_receive_the_read_token_when_only_the_read_supplies_one()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstStreamBehavior<,>))
            .AddHandler<CountdownHandler>());
        using var cancellation = new CancellationTokenSource();

        await Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1)), cancellation.Token);

        await Assert.That(host.Journal.Captured<ObservedToken>().Single().Token).IsEqualTo(cancellation.Token);
        await Assert.That(host.Journal.Captured<CancellationToken>().Single()).IsEqualTo(cancellation.Token);
    }

    [Test]
    public async Task Same_token_supplied_twice_is_passed_on_unchanged()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<CountdownHandler>());
        using var cancellation = new CancellationTokenSource();

        await Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1), cancellation.Token), cancellation.Token);

        await Assert.That(host.Journal.Captured<CancellationToken>().Single()).IsEqualTo(cancellation.Token);
    }

    [Test]
    public async Task Components_receive_a_token_canceled_by_either_source_when_the_tokens_differ()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<CountdownHandler>());
        using var streamCancellation = new CancellationTokenSource();
        using var readCancellation = new CancellationTokenSource();
        IAsyncEnumerable<Tick> stream = host.Sender.StreamAsync(new Countdown(2), streamCancellation.Token);
        await using IAsyncEnumerator<Tick> reader = stream.GetAsyncEnumerator(readCancellation.Token);

        await reader.MoveNextAsync();
        CancellationToken received = host.Journal.Captured<CancellationToken>().Single();
        await readCancellation.CancelAsync();

        await Assert.That(received).IsNotEqualTo(streamCancellation.Token);
        await Assert.That(received).IsNotEqualTo(readCancellation.Token);
        await Assert.That(received.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task Canceling_the_stream_token_during_a_read_cancels_the_linked_token()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<CountdownHandler>());
        using var streamCancellation = new CancellationTokenSource();
        using var readCancellation = new CancellationTokenSource();
        IAsyncEnumerable<Tick> stream = host.Sender.StreamAsync(new Countdown(2), streamCancellation.Token);
        await using IAsyncEnumerator<Tick> reader = stream.GetAsyncEnumerator(readCancellation.Token);

        await reader.MoveNextAsync();
        CancellationToken received = host.Journal.Captured<CancellationToken>().Single();
        await streamCancellation.CancelAsync();

        await Assert.That(received.IsCancellationRequested).IsTrue();
        await Assert.That(readCancellation.IsCancellationRequested).IsFalse();
    }

    [Test]
    public async Task Linked_token_is_released_when_the_read_ends()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<CountdownHandler>());
        using var streamCancellation = new CancellationTokenSource();
        using var readCancellation = new CancellationTokenSource();

        await Streams.ReadAllAsync(
            host.Sender.StreamAsync(new Countdown(1), streamCancellation.Token),
            readCancellation.Token);
        await streamCancellation.CancelAsync();

        await Assert.That(host.Journal.Captured<CancellationToken>().Single().IsCancellationRequested).IsFalse();
    }

    [Test]
    public async Task Token_is_not_checked_between_items()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstStreamBehavior<,>))
            .AddHandler<CountdownHandler>());
        using var cancellation = new CancellationTokenSource();
        List<int> values = [];

        await foreach (Tick tick in host.Sender.StreamAsync(new Countdown(3), cancellation.Token))
        {
            values.Add(tick.Value);
            await cancellation.CancelAsync();
        }

        await Assert.That(values).IsEquivalentTo([3, 2, 1]);
    }

    [Test]
    public async Task Behavior_may_substitute_the_token_for_the_rest_of_the_pipeline()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<SubstitutingBehavior>()
            .AddHandler<CountdownHandler>());
        using var cancellation = new CancellationTokenSource();

        await Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1), cancellation.Token));

        CancellationToken substitute = host.Journal.Captured<ObservedToken>().Single().Token;
        await Assert.That(host.Journal.Captured<CancellationToken>().Single()).IsEqualTo(substitute);
        await Assert.That(substitute).IsNotEqualTo(cancellation.Token);
    }

    public sealed class SubstitutingBehavior(Journal journal) : IStreamPipelineBehavior<Countdown, Tick>
    {
        public async IAsyncEnumerable<Tick> HandleAsync(
            Countdown request,
            StreamContinuation<Tick> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var substitute = new CancellationTokenSource();
            journal.Capture(new ObservedToken("substitute", substitute.Token));

            await foreach (Tick tick in next(substitute.Token))
            {
                yield return tick;
            }
        }
    }
}
