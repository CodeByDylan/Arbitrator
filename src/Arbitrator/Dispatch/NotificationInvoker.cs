using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Arbitrator.Dispatch;

/// <summary>
/// Publishes one notification type: resolves its handlers from a scope and invokes them one after another.
/// </summary>
internal abstract class NotificationInvoker
{
    /// <summary>
    /// Gets the handler types this invoker resolves from a scope, in invocation order.
    /// </summary>
    public abstract IReadOnlyList<Type> ComponentTypes { get; }

    /// <summary>
    /// Creates an invoker for a notification type with no handlers, to be completed with <see cref="WithHandlers"/>
    /// once all registrations are known.
    /// </summary>
    /// <param name="notificationType">The notification type.</param>
    /// <returns>An invoker without handlers.</returns>
    [RequiresDynamicCode("Closes the invoker type over the notification type at runtime.")]
    public static NotificationInvoker CreateTemplate(Type notificationType)
    {
        Type invokerType = typeof(NotificationInvoker<>).MakeGenericType(notificationType);

        // Activator returns null only for Nullable<T>, which an invoker type never is.
        return (NotificationInvoker)Activator.CreateInstance(invokerType, ImmutableArray<Type>.Empty)!;
    }

    /// <summary>
    /// Creates an invoker for the same notification type that runs the given handlers in order.
    /// </summary>
    /// <param name="handlerTypes">The handler types, in registration order.</param>
    /// <returns>A new invoker.</returns>
    public abstract NotificationInvoker WithHandlers(ImmutableArray<Type> handlerTypes);

    /// <summary>
    /// Resolves the handlers from a scope and invokes them one after another.
    /// </summary>
    /// <param name="notification">
    /// The notification, whose runtime type matches this invoker's notification type.
    /// </param>
    /// <param name="services">The scope's service provider.</param>
    /// <param name="cancellationToken">The token passed to every handler and checked before each one runs.</param>
    /// <returns>A task that completes when the last handler has completed.</returns>
    public abstract Task PublishAsync(
        INotification notification,
        IServiceProvider services,
        CancellationToken cancellationToken);
}
