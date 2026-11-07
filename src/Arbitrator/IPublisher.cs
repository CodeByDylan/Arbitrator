namespace Arbitrator;

/// <summary>
/// Publishes notifications to their handlers, using the dependency injection scope that resolved the publisher.
/// </summary>
/// <remarks>
/// Handlers run one after another, in registration order, and each is awaited before the next starts. Publishing a
/// notification that has no handlers succeeds. Every failure is reported through the returned task.
/// </remarks>
public interface IPublisher
{
    /// <summary>
    /// Publishes a notification to every handler registered for its exact runtime type.
    /// </summary>
    /// <param name="notification">The notification. Its exact runtime type selects the handlers.</param>
    /// <param name="cancellationToken">
    /// The token passed to every handler, and checked before the handlers are resolved and before each one runs.
    /// </param>
    /// <returns>A task that completes when the last handler has completed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="notification"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled before a handler started.
    /// </exception>
    Task PublishAsync(INotification notification, CancellationToken cancellationToken = default);
}
