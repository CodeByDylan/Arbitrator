using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Arbitrator.Dispatch;

/// <summary>
/// Runs the pipeline of one request type: resolves its behaviors and handler from a scope and invokes them in order.
/// </summary>
internal abstract class PipelineInvoker
{
    /// <summary>
    /// Creates an invoker for a handler and its behaviors.
    /// </summary>
    /// <param name="handlerType">The handler implementation type.</param>
    /// <param name="behaviorTypes">The closed behavior types, in registration order.</param>
    protected PipelineInvoker(Type handlerType, ImmutableArray<Type> behaviorTypes)
    {
        HandlerType = handlerType;
        BehaviorTypes = behaviorTypes;
    }

    /// <summary>
    /// Gets the handler and behavior types this invoker resolves from a scope.
    /// </summary>
    public IReadOnlyList<Type> ComponentTypes => [HandlerType, .. BehaviorTypes];

    /// <summary>
    /// Gets the handler implementation type.
    /// </summary>
    protected Type HandlerType { get; }

    /// <summary>
    /// Gets the closed behavior types, outermost first.
    /// </summary>
    protected ImmutableArray<Type> BehaviorTypes { get; }

    /// <summary>
    /// Creates an invoker for a handler contract with an empty pipeline, to be completed with
    /// <see cref="WithBehaviors"/> once all registrations are known.
    /// </summary>
    /// <param name="kind">The kind of pipeline the contract belongs to.</param>
    /// <param name="requestType">The request type.</param>
    /// <param name="responseType">The response type, or the item type of a stream request.</param>
    /// <param name="handlerType">The handler implementation type.</param>
    /// <returns>An invoker without behaviors.</returns>
    [RequiresDynamicCode("Closes the invoker type over the request and response types at runtime.")]
    public static PipelineInvoker CreateTemplate(
        PipelineKind kind,
        Type requestType,
        Type responseType,
        Type handlerType)
    {
        Type invokerDefinition = kind == PipelineKind.Stream ? typeof(StreamInvoker<,>) : typeof(RequestInvoker<,>);
        Type invokerType = invokerDefinition.MakeGenericType(requestType, responseType);

        // Activator returns null only for Nullable<T>, which an invoker type never is.
        return (PipelineInvoker)Activator.CreateInstance(invokerType, handlerType, ImmutableArray<Type>.Empty)!;
    }

    /// <summary>
    /// Resolves this pipeline's behaviors from a scope, outermost first.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior contract of the pipeline.</typeparam>
    /// <param name="services">The scope's service provider.</param>
    /// <returns>The behavior instances, outermost first.</returns>
    protected TBehavior[] ResolveBehaviors<TBehavior>(IServiceProvider services)
    {
        return ComponentResolver.ResolveBehaviors<TBehavior>(
            services,
            BehaviorTypes,
            ArbitratorServiceKey.RequestComponents);
    }

    /// <summary>
    /// Resolves this pipeline's handler from a scope.
    /// </summary>
    /// <typeparam name="THandler">The handler contract of the pipeline.</typeparam>
    /// <param name="services">The scope's service provider.</param>
    /// <returns>The handler instance.</returns>
    protected THandler ResolveHandler<THandler>(IServiceProvider services)
    {
        return ComponentResolver.Resolve<THandler>(services, HandlerType, ArbitratorServiceKey.RequestComponents);
    }

    /// <summary>
    /// Creates an invoker for the same handler that runs the given behaviors, outermost first.
    /// </summary>
    /// <param name="behaviorTypes">The closed behavior types, in registration order.</param>
    /// <returns>A new invoker.</returns>
    public abstract PipelineInvoker WithBehaviors(ImmutableArray<Type> behaviorTypes);
}
