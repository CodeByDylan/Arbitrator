using System.Collections.Immutable;

namespace Arbitrator.Dispatch;

/// <summary>
/// Runs the pipeline of one stream request type with statically typed behaviors and handler.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TItem">The item type.</typeparam>
internal sealed class StreamInvoker<TRequest, TItem> : StreamInvoker<TItem>
    where TRequest : IStreamRequest<TItem>
{
    /// <summary>
    /// Creates an invoker for a handler and an ordered set of behaviors.
    /// </summary>
    /// <param name="handlerType">The handler implementation type, registered under the Arbitrator service key.</param>
    /// <param name="behaviorTypes">The closed behavior types, outermost first.</param>
    public StreamInvoker(Type handlerType, ImmutableArray<Type> behaviorTypes)
        : base(handlerType, behaviorTypes)
    {
    }

    /// <inheritdoc/>
    public override PipelineInvoker WithBehaviors(ImmutableArray<Type> behaviorTypes)
    {
        return new StreamInvoker<TRequest, TItem>(HandlerType, behaviorTypes);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Every behavior is resolved, outermost first, and then the handler, before any of them runs.
    /// </remarks>
    public override IAsyncEnumerable<TItem> Open(
        IStreamRequest<TItem> request,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;
        IStreamPipelineBehavior<TRequest, TItem>[] behaviors =
            ComponentResolver.ResolveBehaviors<IStreamPipelineBehavior<TRequest, TItem>>(services, BehaviorTypes);
        var handler = ComponentResolver.Resolve<IStreamRequestHandler<TRequest, TItem>>(services, HandlerType);

        if (behaviors.Length == 0)
        {
            return ComponentStreams.FromHandler(handler.HandleAsync(typedRequest, cancellationToken), handler);
        }

        return OpenPipeline(typedRequest, behaviors, handler, cancellationToken);
    }

    /// <summary>
    /// Chains the behaviors around the handler and invokes the outermost behavior.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="behaviors">The behavior instances, outermost first. Contains at least one behavior.</param>
    /// <param name="handler">The handler instance.</param>
    /// <param name="cancellationToken">The token passed to the outermost behavior.</param>
    /// <returns>The stream of the outermost behavior.</returns>
    private static IAsyncEnumerable<TItem> OpenPipeline(
        TRequest request,
        IStreamPipelineBehavior<TRequest, TItem>[] behaviors,
        IStreamRequestHandler<TRequest, TItem> handler,
        CancellationToken cancellationToken)
    {
        StreamContinuation<TItem> next =
            new StreamHandlerContinuation<TRequest, TItem>(behaviors[^1].GetType(), handler, request).InvokeAsync;

        for (int index = behaviors.Length - 1; index > 0; index--)
        {
            Type ownerType = behaviors[index - 1].GetType();
            var continuation = new StreamBehaviorContinuation<TRequest, TItem>(
                ownerType,
                behaviors[index],
                request,
                next);
            next = continuation.InvokeAsync;
        }

        IStreamPipelineBehavior<TRequest, TItem> outermost = behaviors[0];
        return ComponentStreams.FromBehavior(outermost.HandleAsync(request, next, cancellationToken), outermost);
    }
}
