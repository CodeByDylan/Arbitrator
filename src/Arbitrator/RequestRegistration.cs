using System.Collections.Immutable;
using Arbitrator.Dispatch;
using Arbitrator.Registration;

namespace Arbitrator;

/// <summary>
/// Describes one request type: its handler and the behaviors that run around it.
/// </summary>
public sealed class RequestRegistration
{
    /// <summary>
    /// Creates the description of a registered handler contract and its pipeline.
    /// </summary>
    /// <param name="contract">The handler contract.</param>
    /// <param name="handler">The handler and its lifetime.</param>
    /// <param name="behaviors">The behaviors that run for the request and their lifetimes, outermost first.</param>
    internal RequestRegistration(
        HandlerContract contract,
        ComponentRegistration handler,
        ImmutableArray<ComponentRegistration> behaviors)
    {
        RequestType = contract.RequestType;
        ResponseType = contract.ResponseType;
        IsStream = contract.Kind == PipelineKind.Stream;
        Handler = handler;
        Behaviors = behaviors;
        BehaviorTypes = behaviors.Select(behavior => behavior.ImplementationType).ToImmutableArray();
    }

    /// <summary>
    /// Gets the exact request type that dispatch selects this registration for.
    /// </summary>
    public Type RequestType { get; }

    /// <summary>
    /// Gets the type the request produces: the <c>TResponse</c> of <see cref="IRequest{TResponse}"/>,
    /// <see cref="Unit"/> for a request implementing <see cref="IRequest"/>, or the <c>TItem</c> of
    /// <see cref="IStreamRequest{TItem}"/>.
    /// </summary>
    public Type ResponseType { get; }

    /// <summary>
    /// Gets a value indicating whether the request implements <see cref="IStreamRequest{TItem}"/>.
    /// </summary>
    public bool IsStream { get; }

    /// <summary>
    /// Gets the handler implementation type, which is the implementation type of <see cref="Handler"/>.
    /// </summary>
    public Type HandlerType => Handler.ImplementationType;

    /// <summary>
    /// Gets the handler and the lifetime it is registered with in its request role.
    /// </summary>
    public ComponentRegistration Handler { get; }

    /// <summary>
    /// Gets the closed behavior types that run for the request, outermost first. An open behavior appears as the
    /// instantiation that runs.
    /// </summary>
    public IReadOnlyList<Type> BehaviorTypes { get; }

    /// <summary>
    /// Gets the behaviors that run for the request and their lifetimes, in the same order as
    /// <see cref="BehaviorTypes"/>.
    /// </summary>
    public IReadOnlyList<ComponentRegistration> Behaviors { get; }
}
