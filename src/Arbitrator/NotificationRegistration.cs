using System.Collections.Immutable;

namespace Arbitrator;

/// <summary>
/// Describes one notification type and the handlers that run for it.
/// </summary>
public sealed class NotificationRegistration
{
    /// <summary>
    /// Creates the description of a notification type and its handlers.
    /// </summary>
    /// <param name="notificationType">The notification type.</param>
    /// <param name="handlerTypes">The handler types, in the order they run.</param>
    internal NotificationRegistration(Type notificationType, ImmutableArray<Type> handlerTypes)
    {
        NotificationType = notificationType;
        HandlerTypes = handlerTypes;
    }

    /// <summary>
    /// Gets the exact notification type.
    /// </summary>
    public Type NotificationType { get; }

    /// <summary>
    /// Gets the handler implementation types, in the order they run.
    /// </summary>
    public IReadOnlyList<Type> HandlerTypes { get; }
}
