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
    /// <param name="behaviorTypes">The closed behavior types that run for the request, outermost first.</param>
    internal RequestRegistration(HandlerContract contract, ImmutableArray<Type> behaviorTypes)
    {
        RequestType = contract.RequestType;
        ResponseType = contract.ResponseType;
        IsStream = contract.Kind == PipelineKind.Stream;
        HandlerType = contract.HandlerType;
        BehaviorTypes = behaviorTypes;
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
    /// Gets the handler implementation type.
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// Gets the closed behavior types that run for the request, outermost first. An open behavior appears as the
    /// instantiation that runs.
    /// </summary>
    public IReadOnlyList<Type> BehaviorTypes { get; }
}
