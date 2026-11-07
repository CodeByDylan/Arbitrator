using System.Collections.Concurrent;
using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Concurrency;

/// <summary>
/// Specifies that concurrent sends share no request-specific state.
/// </summary>
public sealed class ConcurrencyTests
{
    private const int ScopeCount = 1_000;
    private const int SendsPerScope = 100;
    private const int FirstSendCount = 64;

    [Test]
    public async Task Concurrent_sends_in_separate_scopes_each_receive_their_own_response()
    {
        await using TestHost host = CreateHost();
        ConcurrentBag<string> mismatches = [];

        await Parallel.ForEachAsync(Enumerable.Range(0, ScopeCount), async (index, cancellationToken) =>
        {
            await using AsyncServiceScope scope = host.Provider.CreateAsyncScope();
            string message = $"{index}:{Guid.NewGuid()}";
            Pong response = await scope.ServiceProvider.GetRequiredService<ISender>()
                .SendAsync(new Ping(message), cancellationToken);

            if (response.Message != message)
            {
                mismatches.Add(message);
            }
        });

        await Assert.That(mismatches).IsEmpty();
        await Assert.That(CountDistinct(host.Journal.Captured<InstanceCapturingBehavior<Ping, Pong>>()))
            .IsEqualTo(ScopeCount);
    }

    [Test]
    public async Task Concurrent_sends_in_one_scope_use_distinct_handler_instances()
    {
        await using TestHost host = CreateHost();
        ISender sender = host.Sender;

        Pong[] responses = await Task.WhenAll(Enumerable.Range(0, SendsPerScope)
            .Select(index => Task.Run(() => sender.SendAsync(new Ping($"{index}")))));

        await Assert.That(responses.Select(response => response.Message))
            .IsEquivalentTo(Enumerable.Range(0, SendsPerScope).Select(index => $"{index}"));
        await Assert.That(CountDistinct(host.Journal.Captured<PingHandler>())).IsEqualTo(SendsPerScope);
    }

    [Test]
    public async Task Concurrent_first_sends_on_a_new_provider_all_succeed()
    {
        await using TestHost host = CreateHost();

        Pong[] responses = await Task.WhenAll(Enumerable.Range(0, FirstSendCount).Select(index => Task.Run(
            async () =>
            {
                await using AsyncServiceScope scope = host.Provider.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new Ping($"{index}"));
            })));

        await Assert.That(responses.Select(response => response.Message))
            .IsEquivalentTo(Enumerable.Range(0, FirstSendCount).Select(index => $"{index}"));
        await Expect.EntriesAsync(host.Journal, [.. Enumerable.Repeat("handler", FirstSendCount)]);
    }

    private static TestHost CreateHost()
    {
        return TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(InstanceCapturingBehavior<,>))
            .AddHandler<PingHandler>());
    }

    private static int CountDistinct<T>(IReadOnlyList<T> instances)
        where T : class
    {
        return instances.Distinct(ReferenceEqualityComparer.Instance).Count();
    }

    public sealed class InstanceCapturingBehavior<TRequest, TResponse>(Journal journal)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request,
            RequestContinuation<TResponse> next,
            CancellationToken cancellationToken)
        {
            journal.Capture(this);
            return next(cancellationToken);
        }
    }
}
