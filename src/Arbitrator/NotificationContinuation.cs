namespace Arbitrator;

/// <summary>
/// Invokes the rest of the chain around one notification handler: the next behavior, or the handler itself.
/// </summary>
/// <remarks>
/// A continuation may be invoked at most once.
/// </remarks>
/// <param name="cancellationToken">The token passed to every remaining behavior and to the handler.</param>
/// <returns>A task that completes when the rest of the chain is done.</returns>
/// <exception cref="InvalidOperationException">The continuation has already been invoked.</exception>
public delegate Task NotificationContinuation(CancellationToken cancellationToken);
