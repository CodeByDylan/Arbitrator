namespace Arbitrator;

/// <summary>
/// Handles one notification type. Any number of handlers may handle the same notification type.
/// </summary>
/// <typeparam name="TNotification">The exact notification type this handler handles.</typeparam>
public interface INotificationHandler<TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles a notification.
    /// </summary>
    /// <param name="notification">The notification to handle.</param>
    /// <param name="cancellationToken">The token that signals publication should be abandoned.</param>
    /// <returns>A task that completes when the notification has been handled.</returns>
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken);
}
