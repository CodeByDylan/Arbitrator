using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Dispatch;

/// <summary>
/// Runs the pipeline of one request type with statically typed behaviors and handler.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class RequestInvoker<TRequest, TResponse> : RequestInvoker<TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly Type _handlerType;
    private readonly ImmutableArray<Type> _behaviorTypes;

    /// <summary>
    /// Creates an invoker for a handler and an ordered set of behaviors.
    /// </summary>
    /// <param name="handlerType">The handler implementation type, registered under the Arbitrator service key.</param>
    /// <param name="behaviorTypes">The closed behavior types, outermost first.</param>
    public RequestInvoker(Type handlerType, ImmutableArray<Type> behaviorTypes)
    {
        _handlerType = handlerType;
        _behaviorTypes = behaviorTypes;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<Type> ComponentTypes => [_handlerType, .. _behaviorTypes];

    /// <inheritdoc/>
    public override RequestInvoker WithBehaviors(ImmutableArray<Type> behaviorTypes)
    {
        return new RequestInvoker<TRequest, TResponse>(_handlerType, behaviorTypes);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Every behavior is resolved, outermost first, and then the handler, before any of them runs.
    /// </remarks>
    public override Task<TResponse> InvokeAsync(
        IRequest<TResponse> request,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;
        IPipelineBehavior<TRequest, TResponse>[] behaviors = ResolveBehaviors(services);
        var handler = (IRequestHandler<TRequest, TResponse>)services.GetRequiredKeyedService(
            _handlerType,
            ArbitratorServiceKey.RequestComponents);

        if (behaviors.Length == 0)
        {
            return ComponentTasks.FromHandler(handler.HandleAsync(typedRequest, cancellationToken), handler);
        }

        return RunPipeline(typedRequest, behaviors, handler, cancellationToken);
    }

    /// <summary>
    /// Resolves the behaviors from a scope, outermost first.
    /// </summary>
    /// <param name="services">The scope's service provider.</param>
    /// <returns>The behavior instances, outermost first.</returns>
    private IPipelineBehavior<TRequest, TResponse>[] ResolveBehaviors(IServiceProvider services)
    {
        var behaviors = new IPipelineBehavior<TRequest, TResponse>[_behaviorTypes.Length];

        for (int index = 0; index < behaviors.Length; index++)
        {
            behaviors[index] = (IPipelineBehavior<TRequest, TResponse>)services.GetRequiredKeyedService(
                _behaviorTypes[index],
                ArbitratorServiceKey.RequestComponents);
        }

        return behaviors;
    }

    /// <summary>
    /// Chains the behaviors around the handler and invokes the outermost behavior.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="behaviors">The behavior instances, outermost first. Contains at least one behavior.</param>
    /// <param name="handler">The handler instance.</param>
    /// <param name="cancellationToken">The token passed to the outermost behavior.</param>
    /// <returns>A task that produces the response of the outermost behavior.</returns>
    private static Task<TResponse> RunPipeline(
        TRequest request,
        IPipelineBehavior<TRequest, TResponse>[] behaviors,
        IRequestHandler<TRequest, TResponse> handler,
        CancellationToken cancellationToken)
    {
        RequestContinuation<TResponse> next =
            new HandlerContinuation<TRequest, TResponse>(behaviors[^1].GetType(), handler, request).InvokeAsync;

        for (int index = behaviors.Length - 1; index > 0; index--)
        {
            Type ownerType = behaviors[index - 1].GetType();
            var continuation = new BehaviorContinuation<TRequest, TResponse>(
                ownerType,
                behaviors[index],
                request,
                next);
            next = continuation.InvokeAsync;
        }

        IPipelineBehavior<TRequest, TResponse> outermost = behaviors[0];
        return ComponentTasks.FromBehavior(outermost.HandleAsync(request, next, cancellationToken), outermost);
    }
}
