namespace Arbitrator;

/// <summary>
/// Marks a notification that is published through <see cref="IPublisher"/> to every handler registered for it.
/// </summary>
/// <remarks>
/// Notifications are published by their exact runtime type, so a handler registered for a base type never receives a
/// derived notification.
/// </remarks>
public interface INotification;
