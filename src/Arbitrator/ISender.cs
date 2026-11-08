namespace Arbitrator;

/// <summary>
/// Sends requests to their handlers through the request pipeline, using the dependency injection scope that
/// resolved the sender.
/// </summary>
/// <remarks>
/// Every failure, including invalid arguments and missing handlers, is reported through the returned task.
/// </remarks>
public interface ISender
{
    /// <summary>
    /// Sends a request that produces a response.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response the request produces.</typeparam>
    /// <param name="request">The request. Its exact runtime type selects the handler and the pipeline.</param>
    /// <param name="cancellationToken">The token forwarded to every behavior and to the handler.</param>
    /// <returns>A task that produces the response returned by the pipeline.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArbitratorConfigurationException">No handler is registered for the request type.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled before dispatch started.
    /// </exception>
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request that produces no response.
    /// </summary>
    /// <param name="request">The request. Its exact runtime type selects the handler and the pipeline.</param>
    /// <param name="cancellationToken">The token forwarded to every behavior and to the handler.</param>
    /// <returns>A task that completes when the pipeline completes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArbitratorConfigurationException">No handler is registered for the request type.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled before dispatch started.
    /// </exception>
    Task SendAsync(IRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a stream of the items a stream request produces through the stream pipeline.
    /// </summary>
    /// <remarks>
    /// Calling this method does nothing and never throws. Each read of the returned stream is a separate dispatch,
    /// and every failure, including <see cref="ArgumentNullException"/> for a null request,
    /// <see cref="ArbitratorConfigurationException"/> for a missing handler, and
    /// <see cref="OperationCanceledException"/> for a token canceled before the read started, is thrown by the first
    /// <see cref="IAsyncEnumerator{T}.MoveNextAsync"/> of that read. When a read supplies its own token, the read
    /// observes both tokens.
    /// </remarks>
    /// <typeparam name="TItem">The type of the items the request produces.</typeparam>
    /// <param name="request">The request. Its exact runtime type selects the handler and the pipeline.</param>
    /// <param name="cancellationToken">The token forwarded to every behavior and to the handler.</param>
    /// <returns>The items produced by the pipeline.</returns>
    IAsyncEnumerable<TItem> StreamAsync<TItem>(
        IStreamRequest<TItem> request,
        CancellationToken cancellationToken = default);
}
