namespace Arbitrator.Dispatch;

/// <summary>
/// A continuation that runs the next behavior around a notification handler.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
internal sealed class NotificationBehaviorContinuation<TNotification> : GuardedContinuation<TNotification, Task>
    where TNotification : INotification
{
    private readonly INotificationBehavior<TNotification> _behavior;
    private readonly NotificationHandlerCall<TNotification> _call;
    private readonly NotificationContinuation _next;

    /// <summary>
    /// Creates a continuation that runs a behavior with its own continuation.
    /// </summary>
    /// <param name="ownerType">The behavior that receives this continuation.</param>
    /// <param name="behavior">The behavior to run.</param>
    /// <param name="call">The handler call the behavior wraps.</param>
    /// <param name="next">The continuation handed to <paramref name="behavior"/>.</param>
    public NotificationBehaviorContinuation(
        Type ownerType,
        INotificationBehavior<TNotification> behavior,
        NotificationHandlerCall<TNotification> call,
        NotificationContinuation next)
        : base(ownerType)
    {
        _behavior = behavior;
        _call = call;
        _next = next;
    }

    /// <inheritdoc/>
    protected override Task InvokeNextAsync(CancellationToken cancellationToken)
    {
        return _behavior.HandleAsync(_call.Notification, _call.HandlerType, _next, cancellationToken)
            ?? throw DispatchErrors.NullBehaviorTask(_behavior.GetType(), typeof(TNotification));
    }
}
