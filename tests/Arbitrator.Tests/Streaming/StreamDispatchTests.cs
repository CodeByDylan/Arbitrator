using System.Runtime.CompilerServices;
using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Streaming;

/// <summary>
/// Specifies how stream requests reach their handler and how their items reach the caller.
/// </summary>
public sealed class StreamDispatchTests
{
    [Test]
    public async Task Stream_delivers_every_item_in_order_as_the_same_instances()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<CountdownHandler>());

        List<Tick> items = await Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(3)));

        await Assert.That(items.Select(item => item.Value)).IsEquivalentTo([3, 2, 1]);
        await Assert.That(items.SequenceEqual(host.Journal.Captured<Tick>(), ReferenceEqualityComparer.Instance))
            .IsTrue();
    }

    [Test]
    public async Task Handler_receives_the_callers_request_instance_and_token()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<CountdownHandler>());
        using var cancellation = new CancellationTokenSource();
        var request = new Countdown(1);

        await Streams.ReadAllAsync(host.Sender.StreamAsync(request, cancellation.Token));

        await Assert.That(host.Journal.Captured<Countdown>().Single()).IsSameReferenceAs(request);
        await Assert.That(host.Journal.Captured<CancellationToken>().Single()).IsEqualTo(cancellation.Token);
    }

    [Test]
    public async Task Struct_stream_request_is_dispatched_by_its_runtime_type()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<RepeatHandler>());

        List<int> items = await Streams.ReadAllAsync(host.Sender.StreamAsync(new Repeat(3)));

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task Request_held_through_an_interface_reference_is_dispatched_by_its_runtime_type()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<CountdownHandler>());
        IStreamRequest<Tick> request = new Countdown(2);

        List<Tick> items = await Streams.ReadAllAsync(host.Sender.StreamAsync(request));

        await Assert.That(items.Select(item => item.Value)).IsEquivalentTo([2, 1]);
    }

    [Test]
    public async Task One_class_handles_a_request_and_a_stream_request_with_a_new_instance_each_time()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<DualHandler>());

        Pong response = await host.Sender.SendAsync(new Ping("hello"));
        List<Tick> items = await Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1)));

        await Assert.That(response.Message).IsEqualTo("hello");
        await Assert.That(items.Single().Value).IsEqualTo(1);
        await Assert.That(host.Journal.Captured<DualHandler>().Distinct().Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Stream_infers_the_item_type_from_the_request()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<CountdownHandler>());
        IStreamRequest<Tick> throughInterface = new Countdown(1);

        Type concrete = StaticType.OfResult(() => host.Sender.StreamAsync(new Countdown(1)));
        Type viaInterface = StaticType.OfResult(() => host.Sender.StreamAsync(throughInterface));
        Type structRequest = StaticType.OfResult(() => host.Sender.StreamAsync(new Repeat(1)));

        await Assert.That(concrete).IsEqualTo(typeof(IAsyncEnumerable<Tick>));
        await Assert.That(viaInterface).IsEqualTo(typeof(IAsyncEnumerable<Tick>));
        await Assert.That(structRequest).IsEqualTo(typeof(IAsyncEnumerable<int>));
    }

    public readonly record struct Repeat(int Times) : IStreamRequest<int>;

    public sealed class RepeatHandler : IStreamRequestHandler<Repeat, int>
    {
        public async IAsyncEnumerable<int> HandleAsync(
            Repeat request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int value = 1; value <= request.Times; value++)
            {
                yield return value;
            }
        }
    }

    public sealed class DualHandler(Journal journal)
        : IRequestHandler<Ping, Pong>, IStreamRequestHandler<Countdown, Tick>
    {
        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            journal.Capture(this);
            return Task.FromResult(new Pong(request.Message));
        }

        public async IAsyncEnumerable<Tick> HandleAsync(
            Countdown request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            journal.Capture(this);
            yield return new Tick(request.From);
        }
    }
}
