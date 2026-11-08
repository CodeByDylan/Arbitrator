namespace Arbitrator.Dispatch;

/// <summary>
/// The innermost continuation around a notification handler, which runs the handler.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
internal sealed class NotificationHandlerContinuation<TNotification> : GuardedContinuation<TNotification, Task>
    where TNotification : INotification
{
    private readonly INotificationHandler<TNotification> _handler;
    private readonly TNotification _notification;

    /// <summary>
    /// Creates a continuation that runs a handler.
    /// </summary>
    /// <param name="ownerType">The innermost behavior, which receives this continuation.</param>
    /// <param name="handler">The handler to run.</param>
    /// <param name="notification">The notification.</param>
    public NotificationHandlerContinuation(
        Type ownerType,
        INotificationHandler<TNotification> handler,
        TNotification notification)
        : base(ownerType)
    {
        _handler = handler;
        _notification = notification;
    }

    /// <inheritdoc/>
    protected override Task InvokeNextAsync(CancellationToken cancellationToken)
    {
        return _handler.HandleAsync(_notification, cancellationToken)
            ?? throw DispatchErrors.NullHandlerTask(_handler.GetType(), typeof(TNotification));
    }
}
