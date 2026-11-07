using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Dispatch;

/// <summary>
/// Publishes one notification type to statically typed handlers.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
internal sealed class NotificationInvoker<TNotification> : NotificationInvoker
    where TNotification : INotification
{
    private readonly ImmutableArray<Type> _handlerTypes;

    /// <summary>
    /// Creates an invoker for an ordered set of handlers.
    /// </summary>
    /// <param name="handlerTypes">The handler types, registered under the Arbitrator service key, in order.</param>
    public NotificationInvoker(ImmutableArray<Type> handlerTypes)
    {
        _handlerTypes = handlerTypes;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<Type> ComponentTypes => _handlerTypes;

    /// <inheritdoc/>
    public override NotificationInvoker WithHandlers(ImmutableArray<Type> handlerTypes)
    {
        return new NotificationInvoker<TNotification>(handlerTypes);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Every handler is resolved before the first one runs.
    /// </remarks>
    public override async Task PublishAsync(
        INotification notification,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var typedNotification = (TNotification)notification;
        INotificationHandler<TNotification>[] handlers = ResolveHandlers(services);

        for (int index = 0; index < handlers.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            INotificationHandler<TNotification> handler = handlers[index];
            Task handling = handler.HandleAsync(typedNotification, cancellationToken)
                ?? throw DispatchErrors.NullHandlerTask(handler.GetType(), typeof(TNotification));
            bool isNextHandlerPending = index < handlers.Length - 1;

            // Only a pending next handler needs the caller's context; resuming on it after the last handler could
            // deadlock a caller that blocks on the publish.
            await handling.ConfigureAwait(continueOnCapturedContext: isNextHandlerPending);
        }
    }

    /// <summary>
    /// Resolves the handlers from a scope, in invocation order.
    /// </summary>
    /// <param name="services">The scope's service provider.</param>
    /// <returns>The handler instances, in invocation order.</returns>
    private INotificationHandler<TNotification>[] ResolveHandlers(IServiceProvider services)
    {
        var handlers = new INotificationHandler<TNotification>[_handlerTypes.Length];

        for (int index = 0; index < handlers.Length; index++)
        {
            handlers[index] = (INotificationHandler<TNotification>)services.GetRequiredKeyedService(
                _handlerTypes[index],
                ArbitratorServiceKey.NotificationHandlers);
        }

        return handlers;
    }
}
