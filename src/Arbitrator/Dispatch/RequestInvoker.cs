using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Arbitrator.Dispatch;

/// <summary>
/// Runs the pipeline of one request type: resolves its behaviors and handler from a scope and invokes them in order.
/// </summary>
internal abstract class RequestInvoker
{
    /// <summary>
    /// Gets the handler and behavior types this invoker resolves from a scope.
    /// </summary>
    public abstract IReadOnlyList<Type> ComponentTypes { get; }

    /// <summary>
    /// Creates an invoker for a handler contract with an empty pipeline, to be completed with
    /// <see cref="WithBehaviors"/> once all registrations are known.
    /// </summary>
    /// <param name="requestType">The request type.</param>
    /// <param name="responseType">The response type.</param>
    /// <param name="handlerType">The handler implementation type.</param>
    /// <returns>An invoker without behaviors.</returns>
    [RequiresDynamicCode("Closes the invoker type over the request and response types at runtime.")]
    public static RequestInvoker CreateTemplate(Type requestType, Type responseType, Type handlerType)
    {
        Type invokerType = typeof(RequestInvoker<,>).MakeGenericType(requestType, responseType);

        // Activator returns null only for Nullable<T>, which an invoker type never is.
        return (RequestInvoker)Activator.CreateInstance(invokerType, handlerType, ImmutableArray<Type>.Empty)!;
    }

    /// <summary>
    /// Creates an invoker for the same handler that runs the given behaviors, outermost first.
    /// </summary>
    /// <param name="behaviorTypes">The closed behavior types, in registration order.</param>
    /// <returns>A new invoker.</returns>
    public abstract RequestInvoker WithBehaviors(ImmutableArray<Type> behaviorTypes);
}
