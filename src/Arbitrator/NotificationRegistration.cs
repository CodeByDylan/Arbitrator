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
    /// <param name="handlers">The handlers and their lifetimes, in the order they run.</param>
    /// <param name="behaviors">The behaviors that wrap each handler and their lifetimes, outermost first.</param>
    internal NotificationRegistration(
        Type notificationType,
        ImmutableArray<ComponentRegistration> handlers,
        ImmutableArray<ComponentRegistration> behaviors)
    {
        NotificationType = notificationType;
        Handlers = handlers;
        Behaviors = behaviors;
        HandlerTypes = handlers.Select(handler => handler.ImplementationType).ToImmutableArray();
        BehaviorTypes = behaviors.Select(behavior => behavior.ImplementationType).ToImmutableArray();
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
    /// Gets the handlers and their lifetimes, in the same order as <see cref="HandlerTypes"/>.
    /// </summary>
    public IReadOnlyList<ComponentRegistration> Handlers { get; }

    /// <summary>
    /// Gets the closed behavior types that run around each handler, outermost first. An open behavior appears as the
    /// instantiation that runs.
    /// </summary>
    public IReadOnlyList<Type> BehaviorTypes { get; }

    /// <summary>
    /// Gets the behaviors that run around each handler and their lifetimes, in the same order as
    /// <see cref="BehaviorTypes"/>.
    /// </summary>
    public IReadOnlyList<ComponentRegistration> Behaviors { get; }
}
