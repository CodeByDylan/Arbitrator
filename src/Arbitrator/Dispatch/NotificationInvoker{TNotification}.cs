using System.Collections.Immutable;

namespace Arbitrator.Dispatch;

/// <summary>
/// Publishes one notification type to statically typed handlers, each inside its own behaviors.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
internal sealed class NotificationInvoker<TNotification> : NotificationInvoker
    where TNotification : INotification
{
    /// <summary>
    /// Creates an invoker for an ordered set of handlers and the behaviors that wrap each of them.
    /// </summary>
    /// <param name="handlerTypes">The handler types, registered under the notification handler key, in order.</param>
    /// <param name="behaviorTypes">
    /// The closed behavior types, registered under the notification behavior key, outermost first.
    /// </param>
    public NotificationInvoker(ImmutableArray<Type> handlerTypes, ImmutableArray<Type> behaviorTypes)
        : base(handlerTypes, behaviorTypes)
    {
    }

    /// <inheritdoc/>
    public override NotificationInvoker WithComponents(
        ImmutableArray<Type> handlerTypes,
        ImmutableArray<Type> behaviorTypes)
    {
        return new NotificationInvoker<TNotification>(handlerTypes, behaviorTypes);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// For each handler in order, its behaviors are resolved, outermost first, and then the handler, before any of
    /// them runs. Each handler gets its own behavior instances.
    /// </remarks>
    public override async Task PublishAsync(
        INotification notification,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var typedNotification = (TNotification)notification;
        ResolvedHandler[] handlers = ResolveHandlers(services);

        for (int index = 0; index < handlers.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task handling = Invoke(typedNotification, handlers[index], cancellationToken);
            bool isNextHandlerPending = index < handlers.Length - 1;

            // Only a pending next handler needs the caller's context; resuming on it after the last handler could
            // deadlock a caller that blocks on the publish.
            await handling.ConfigureAwait(continueOnCapturedContext: isNextHandlerPending);
        }
    }

    /// <summary>
    /// Invokes one handler, directly or through its behaviors.
    /// </summary>
    /// <param name="notification">The notification.</param>
    /// <param name="resolved">The handler and its behaviors.</param>
    /// <param name="cancellationToken">The token passed to the outermost component.</param>
    /// <returns>The task of the outermost component.</returns>
    /// <exception cref="InvalidOperationException">The outermost component returned a null task.</exception>
    private static Task Invoke(
        TNotification notification,
        ResolvedHandler resolved,
        CancellationToken cancellationToken)
    {
        INotificationHandler<TNotification> handler = resolved.Handler;

        if (resolved.Behaviors.Length == 0)
        {
            return handler.HandleAsync(notification, cancellationToken)
                ?? throw DispatchErrors.NullHandlerTask(handler.GetType(), typeof(TNotification));
        }

        var call = new NotificationHandlerCall<TNotification>(notification, handler.GetType());
        return RunChain(call, resolved, cancellationToken);
    }

    /// <summary>
    /// Chains the behaviors around a handler and invokes the outermost behavior.
    /// </summary>
    /// <param name="call">The handler call the behaviors wrap.</param>
    /// <param name="resolved">The handler and its behaviors. Contains at least one behavior.</param>
    /// <param name="cancellationToken">The token passed to the outermost behavior.</param>
    /// <returns>The task of the outermost behavior.</returns>
    /// <exception cref="InvalidOperationException">The outermost behavior returned a null task.</exception>
    private static Task RunChain(
        NotificationHandlerCall<TNotification> call,
        ResolvedHandler resolved,
        CancellationToken cancellationToken)
    {
        INotificationBehavior<TNotification>[] behaviors = resolved.Behaviors;
        NotificationContinuation next = new NotificationHandlerContinuation<TNotification>(
            behaviors[^1].GetType(),
            resolved.Handler,
            call.Notification).InvokeAsync;

        for (int index = behaviors.Length - 1; index > 0; index--)
        {
            Type ownerType = behaviors[index - 1].GetType();
            next = new NotificationBehaviorContinuation<TNotification>(ownerType, behaviors[index], call, next)
                .InvokeAsync;
        }

        INotificationBehavior<TNotification> outermost = behaviors[0];
        return outermost.HandleAsync(call.Notification, call.HandlerType, next, cancellationToken)
            ?? throw DispatchErrors.NullBehaviorTask(outermost.GetType(), typeof(TNotification));
    }

    /// <summary>
    /// Resolves every handler, each preceded by its own behaviors, outermost first.
    /// </summary>
    /// <param name="services">The scope's service provider.</param>
    /// <returns>The handlers and their behaviors, in invocation order.</returns>
    private ResolvedHandler[] ResolveHandlers(IServiceProvider services)
    {
        var handlers = new ResolvedHandler[HandlerTypes.Length];

        for (int index = 0; index < handlers.Length; index++)
        {
            INotificationBehavior<TNotification>[] behaviors =
                ComponentResolver.ResolveBehaviors<INotificationBehavior<TNotification>>(
                    services,
                    BehaviorTypes,
                    ArbitratorServiceKey.NotificationBehaviors);
            var handler = ComponentResolver.Resolve<INotificationHandler<TNotification>>(
                services,
                HandlerTypes[index],
                ArbitratorServiceKey.NotificationHandlers);
            handlers[index] = new ResolvedHandler(handler, behaviors);
        }

        return handlers;
    }

    /// <summary>
    /// A resolved handler and the behaviors that wrap it.
    /// </summary>
    /// <param name="Handler">The handler.</param>
    /// <param name="Behaviors">The behaviors, outermost first.</param>
    private readonly record struct ResolvedHandler(
        INotificationHandler<TNotification> Handler,
        INotificationBehavior<TNotification>[] Behaviors);
}
