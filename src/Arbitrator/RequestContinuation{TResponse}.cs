namespace Arbitrator;

/// <summary>
/// Invokes the remainder of a request pipeline: the next behavior, or the handler at the end of the pipeline.
/// </summary>
/// <remarks>
/// A continuation may be invoked at most once.
/// </remarks>
/// <typeparam name="TResponse">The type of the response the request produces.</typeparam>
/// <param name="cancellationToken">The token passed to every remaining behavior and to the handler.</param>
/// <returns>A task that produces the response of the remainder of the pipeline.</returns>
/// <exception cref="InvalidOperationException">The continuation has already been invoked.</exception>
public delegate Task<TResponse> RequestContinuation<TResponse>(CancellationToken cancellationToken);
