using System.Collections.Immutable;

namespace Arbitrator;

/// <summary>
/// Describes one notification type, the handlers that run for it, and the behaviors that wrap each of them.
/// </summary>
public sealed class NotificationRegistration
{
    /// <summary>
    /// Creates the description of a notification type, its handlers, and its behaviors.
    /// </summary>
    /// <param name="notificationType">The notification type.</param>
    /// <param name="handlerTypes">The handler types, in the order they run.</param>
    /// <param name="behaviorTypes">The closed behavior types that wrap each handler, outermost first.</param>
    internal NotificationRegistration(
        Type notificationType,
        ImmutableArray<Type> handlerTypes,
        ImmutableArray<Type> behaviorTypes)
    {
        NotificationType = notificationType;
        HandlerTypes = handlerTypes;
        BehaviorTypes = behaviorTypes;
    }

    /// <summary>
    /// Gets the exact notification type.
    /// </summary>
    public Type NotificationType { get; }

    /// <summary>
    /// Gets the handler implementation types, in the order they run.
    /// </summary>
    public IReadOnlyList<Type> HandlerTypes { get; }

    /// <summary>
    /// Gets the closed behavior types that run around each handler, outermost first. An open behavior appears as the
    /// instantiation that runs.
    /// </summary>
    public IReadOnlyList<Type> BehaviorTypes { get; }
}
