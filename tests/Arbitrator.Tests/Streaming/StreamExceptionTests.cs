using System.Runtime.CompilerServices;
using Arbitrator.Tests.Support;
using TUnit.Assertions.Enums;

namespace Arbitrator.Tests.Streaming;

/// <summary>
/// Specifies how exceptions raised while a stream is produced reach behaviors and the caller.
/// </summary>
public sealed class StreamExceptionTests
{
    [Test]
    public async Task Exception_after_items_reaches_the_caller_unchanged_after_those_items()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstStreamBehavior<,>))
            .AddHandler<FailingHandler>());
        List<int> values = [];

        StreamFailedException thrown = await Expect.ThrowsExactlyAsync<StreamFailedException>(async () =>
        {
            await foreach (Tick tick in host.Sender.StreamAsync(new Countdown(2)))
            {
                values.Add(tick.Value);
            }
        });

        await Assert.That(thrown).IsSameReferenceAs(host.Journal.Captured<StreamFailedException>().Single());
        await Assert.That(values).IsEquivalentTo([2, 1], CollectionOrdering.Matching);
        await Expect.EntriesAsync(host.Journal, "StreamFirst.before", "StreamFirst.item", "StreamFirst.item");
    }

    [Test]
    public async Task Exception_before_any_item_reaches_the_caller_unchanged()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<FailingHandler>());

        StreamFailedException thrown = await Expect.ThrowsExactlyAsync<StreamFailedException>(
            () => Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(0))));

        await Assert.That(thrown).IsSameReferenceAs(host.Journal.Captured<StreamFailedException>().Single());
    }

    [Test]
    public async Task Exception_from_a_handler_that_is_not_an_iterator_reaches_the_caller_unchanged()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstStreamBehavior<,>))
            .AddHandler<EagerlyFailingHandler>());

        StreamFailedException thrown = await Expect.ThrowsExactlyAsync<StreamFailedException>(
            () => Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1))));

        await Assert.That(thrown).IsSameReferenceAs(host.Journal.Captured<StreamFailedException>().Single());
        await Expect.EntriesAsync(host.Journal, "StreamFirst.before");
    }

    [Test]
    public async Task Behavior_reading_its_enumerator_by_hand_can_translate_an_exception()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<TranslatingBehavior>()
            .AddHandler<FailingHandler>());

        StreamTranslatedException thrown = await Expect.ThrowsExactlyAsync<StreamTranslatedException>(
            () => Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1))));

        await Assert.That(thrown.InnerException)
            .IsSameReferenceAs(host.Journal.Captured<StreamFailedException>().Single());
    }

    public sealed class FailingHandler(Journal journal) : IStreamRequestHandler<Countdown, Tick>
    {
        public async IAsyncEnumerable<Tick> HandleAsync(
            Countdown request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int value = request.From; value > 0; value--)
            {
                yield return new Tick(value);
            }

            var failure = new StreamFailedException();
            journal.Capture(failure);
            throw failure;
        }
    }

    public sealed class EagerlyFailingHandler(Journal journal) : IStreamRequestHandler<Countdown, Tick>
    {
        public IAsyncEnumerable<Tick> HandleAsync(Countdown request, CancellationToken cancellationToken)
        {
            var failure = new StreamFailedException();
            journal.Capture(failure);
            throw failure;
        }
    }

    public sealed class TranslatingBehavior : IStreamPipelineBehavior<Countdown, Tick>
    {
        public async IAsyncEnumerable<Tick> HandleAsync(
            Countdown request,
            StreamContinuation<Tick> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await using IAsyncEnumerator<Tick> items = next(cancellationToken).GetAsyncEnumerator(cancellationToken);

            while (await MoveNextOrTranslateAsync(items))
            {
                yield return items.Current;
            }
        }

        private static async Task<bool> MoveNextOrTranslateAsync(IAsyncEnumerator<Tick> items)
        {
            try
            {
                return await items.MoveNextAsync();
            }
            catch (StreamFailedException exception)
            {
                throw new StreamTranslatedException(exception);
            }
        }
    }

    public sealed class StreamFailedException : Exception
    {
        public StreamFailedException()
            : base("The stream failed.")
        {
        }
    }

    public sealed class StreamTranslatedException : Exception
    {
        public StreamTranslatedException(Exception innerException)
            : base("The stream failed and was translated.", innerException)
        {
        }
    }
}
