using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Arbitrator.Tests.Examples;

/// <summary>
/// Logs the name of every stream request and how many items its read produced, without logging the items.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TItem">The item type.</typeparam>
/// <param name="logger">The logger.</param>
public sealed class StreamLoggingBehavior<TRequest, TItem>(ILogger<StreamLoggingBehavior<TRequest, TItem>> logger)
    : IStreamPipelineBehavior<TRequest, TItem>
    where TRequest : IStreamRequest<TItem>
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<TItem> HandleAsync(
        TRequest request,
        StreamContinuation<TItem> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int count = 0;
        StreamLog.Reading(logger, typeof(TRequest).Name);

        try
        {
            await foreach (TItem item in next(cancellationToken))
            {
                count++;
                yield return item;
            }
        }
        finally
        {
            StreamLog.Read(logger, typeof(TRequest).Name, count);
        }
    }
}
