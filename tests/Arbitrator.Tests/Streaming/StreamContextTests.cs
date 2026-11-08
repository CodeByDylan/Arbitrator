using System.Runtime.CompilerServices;
using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Streaming;

/// <summary>
/// Specifies that every step of a stream starts on the synchronization context the caller requested the item from,
/// even after the previous step finished elsewhere.
/// </summary>
public sealed class StreamContextTests
{
    [Test]
    public async Task Each_step_starts_on_the_context_the_caller_requested_the_item_from()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<ContextObservingHandler>());
        ISender sender = host.Sender;

        await Task.Run(() => ReadOnContext(sender, new MarkedSynchronizationContext()));

        await Expect.EntriesAsync(
            host.Journal,
            "first step on context: True",
            "after leaving the context: False",
            "second step on context: True");
    }

    private static Task ReadOnContext(ISender sender, SynchronizationContext context)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);

        try
        {
            return ReadAllAsync(sender);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static async Task ReadAllAsync(ISender sender)
    {
        await foreach (int _ in sender.StreamAsync(new TwoSteps()))
        {
        }
    }

    public sealed record TwoSteps : IStreamRequest<int>;

    public sealed class ContextObservingHandler(Journal journal) : IStreamRequestHandler<TwoSteps, int>
    {
        public async IAsyncEnumerable<int> HandleAsync(
            TwoSteps request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            journal.Record($"first step on context: {MarkedSynchronizationContext.IsCurrent}");
            await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
            journal.Record($"after leaving the context: {MarkedSynchronizationContext.IsCurrent}");
            yield return 1;

            journal.Record($"second step on context: {MarkedSynchronizationContext.IsCurrent}");
        }
    }
}
