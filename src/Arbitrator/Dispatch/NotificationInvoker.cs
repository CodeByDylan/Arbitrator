using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Arbitrator.Dispatch;

/// <summary>
/// Publishes one notification type: resolves its handlers and their behaviors from a scope and invokes each handler,
/// inside its behaviors, one after another.
/// </summary>
internal abstract class NotificationInvoker
{
    /// <summary>
    /// Creates an invoker for handlers and the behaviors that wrap each of them.
    /// </summary>
    /// <param name="handlerTypes">The handler types, in invocation order.</param>
    /// <param name="behaviorTypes">The closed behavior types that wrap each handler, outermost first.</param>
    protected NotificationInvoker(ImmutableArray<Type> handlerTypes, ImmutableArray<Type> behaviorTypes)
    {
        HandlerTypes = handlerTypes;
        BehaviorTypes = behaviorTypes;
    }

    /// <summary>
    /// Gets the handler types this invoker resolves from a scope, in invocation order.
    /// </summary>
    public ImmutableArray<Type> HandlerTypes { get; }

    /// <summary>
    /// Gets the closed behavior types this invoker resolves around each handler, outermost first.
    /// </summary>
    public ImmutableArray<Type> BehaviorTypes { get; }

    /// <summary>
    /// Creates an invoker for a notification type with no handlers or behaviors, to be completed with
    /// <see cref="WithComponents"/> once all registrations are known.
    /// </summary>
    /// <param name="notificationType">The notification type.</param>
    /// <returns>An invoker without handlers.</returns>
    [RequiresDynamicCode("Closes the invoker type over the notification type at runtime.")]
    public static NotificationInvoker CreateTemplate(Type notificationType)
    {
        Type invokerType = typeof(NotificationInvoker<>).MakeGenericType(notificationType);
        ImmutableArray<Type> none = [];

        // Activator returns null only for Nullable<T>, which an invoker type never is.
        return (NotificationInvoker)Activator.CreateInstance(invokerType, none, none)!;
    }

    /// <summary>
    /// Creates an invoker for the same notification type that runs the given handlers in order, each inside the given
    /// behaviors.
    /// </summary>
    /// <param name="handlerTypes">The handler types, in registration order.</param>
    /// <param name="behaviorTypes">The closed behavior types, outermost first.</param>
    /// <returns>A new invoker.</returns>
    public abstract NotificationInvoker WithComponents(
        ImmutableArray<Type> handlerTypes,
        ImmutableArray<Type> behaviorTypes);

    /// <summary>
    /// Resolves the handlers and their behaviors from a scope and invokes the handlers one after another.
    /// </summary>
    /// <param name="notification">
    /// The notification, whose runtime type matches this invoker's notification type.
    /// </param>
    /// <param name="services">The scope's service provider.</param>
    /// <param name="cancellationToken">The token passed to every component and checked before each handler.</param>
    /// <returns>A task that completes when the last handler has completed.</returns>
    public abstract Task PublishAsync(
        INotification notification,
        IServiceProvider services,
        CancellationToken cancellationToken);
}
