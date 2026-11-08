namespace Arbitrator;

/// <summary>
/// Handles one stream request type and produces its items.
/// </summary>
/// <typeparam name="TRequest">The exact request type this handler handles.</typeparam>
/// <typeparam name="TItem">The type of the items the request produces.</typeparam>
public interface IStreamRequestHandler<TRequest, TItem>
    where TRequest : IStreamRequest<TItem>
{
    /// <summary>
    /// Handles a stream request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">The token that signals the read should be abandoned.</param>
    /// <returns>The items of the request, in the order they are produced.</returns>
    IAsyncEnumerable<TItem> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
