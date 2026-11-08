using System.Runtime.CompilerServices;
using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Streaming;

/// <summary>
/// Specifies the type names that stream-related diagnostic messages contain.
/// </summary>
public sealed class StreamDiagnosticsTests
{
    [Test]
    public async Task Missing_stream_handler_message_names_the_stream_handler_contract()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<PingHandler>());

        ArbitratorConfigurationException exception = await Expect.ThrowsExactlyAsync<ArbitratorConfigurationException>(
            () => Streams.ReadAllAsync(host.Sender.StreamAsync(new Countdown(1))));

        await Assert.That(exception.Message)
            .Contains($"No handler is registered for stream request type '{Names.Of<Countdown>()}'");
        await Assert.That(exception.Message)
            .Contains($"IStreamRequestHandler<{Names.Of<Countdown>()}, {Names.Of<Tick>()}> with AddHandler");
    }

    [Test]
    public async Task Missing_handler_for_a_derived_stream_request_names_the_handled_base_type()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<BaseQueryHandler>());

        ArbitratorConfigurationException exception = await Expect.ThrowsExactlyAsync<ArbitratorConfigurationException>(
            () => Streams.ReadAllAsync(host.Sender.StreamAsync(new DerivedQuery())));

        await Assert.That(exception.Message)
            .Contains($"A handler is registered for base type '{Names.Of<BaseQuery>()}'");
    }

    [Test]
    public async Task Type_without_a_handler_contract_message_names_every_handler_contract()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<Journal>()));

        await Assert.That(exception.Message).Contains("IRequestHandler<TRequest, TResponse>");
        await Assert.That(exception.Message).Contains("IRequestHandler<TRequest>");
        await Assert.That(exception.Message).Contains("IStreamRequestHandler<TRequest, TItem>");
    }

    public record BaseQuery : IStreamRequest<int>;

    public sealed record DerivedQuery : BaseQuery;

    public sealed class BaseQueryHandler : IStreamRequestHandler<BaseQuery, int>
    {
        public async IAsyncEnumerable<int> HandleAsync(
            BaseQuery request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return 1;
        }
    }
}
