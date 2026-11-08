namespace Arbitrator;

/// <summary>
/// Invokes the remainder of a stream pipeline: the next behavior, or the handler at the end of the pipeline.
/// </summary>
/// <remarks>
/// A continuation may be invoked at most once, and the stream it returns may be read at most once.
/// </remarks>
/// <typeparam name="TItem">The type of the items the request produces.</typeparam>
/// <param name="cancellationToken">The token passed to every remaining behavior and to the handler.</param>
/// <returns>The items of the remainder of the pipeline.</returns>
/// <exception cref="InvalidOperationException">The continuation has already been invoked.</exception>
public delegate IAsyncEnumerable<TItem> StreamContinuation<TItem>(CancellationToken cancellationToken);
