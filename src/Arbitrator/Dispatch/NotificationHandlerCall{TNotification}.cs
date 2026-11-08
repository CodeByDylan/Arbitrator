namespace Arbitrator.Dispatch;

/// <summary>
/// One call of a notification handler that behaviors wrap.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
/// <param name="Notification">The notification being published.</param>
/// <param name="HandlerType">The implementation type of the handler.</param>
internal readonly record struct NotificationHandlerCall<TNotification>(TNotification Notification, Type HandlerType)
    where TNotification : INotification;
