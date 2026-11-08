using System.Diagnostics.CodeAnalysis;

namespace Arbitrator;

/// <summary>
/// Wraps each call of a notification handler to add cross-cutting behavior before and after it.
/// </summary>
/// <remarks>
/// When a notification has several handlers, the behavior runs once around each of them. Behaviors run in
/// registration order: the first registered behavior is the outermost. A behavior may substitute the cancellation token
/// passed to <c>next</c>, skip the handler by not invoking <c>next</c>, or catch the handler's exception, in which case
/// publication continues with the next handler.
/// </remarks>
/// <typeparam name="TNotification">The notification type.</typeparam>
public interface INotificationBehavior<TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles one handler call by optionally invoking the rest of its chain.
    /// </summary>
    /// <param name="notification">The notification being published.</param>
    /// <param name="handlerType">The implementation type of the handler this call wraps.</param>
    /// <param name="next">The rest of the chain for this handler. It may be invoked at most once.</param>
    /// <param name="cancellationToken">The token that signals publication should be abandoned.</param>
    /// <returns>A task that completes when the handler call is done.</returns>
    [SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "Named next as in ASP.NET Core middleware; Visual Basic callers can still use it.")]
    Task HandleAsync(
        TNotification notification,
        Type handlerType,
        NotificationContinuation next,
        CancellationToken cancellationToken);
}
