namespace Arbitrator.Dispatch;

/// <summary>
/// A kind of request pipeline, identified by the request, handler, and behavior contracts that define it.
/// </summary>
/// <remarks>
/// A request type implements the request contract of exactly one kind, and behaviors apply only to pipelines of their
/// own kind.
/// </remarks>
internal sealed class PipelineKind
{
    /// <summary>
    /// Creates a pipeline kind.
    /// </summary>
    /// <param name="requestDescription">How messages describe a request type of this kind.</param>
    /// <param name="requestContract">The generic definition of the request contract.</param>
    /// <param name="handlerContract">The generic definition of the handler contract.</param>
    /// <param name="behaviorContract">The generic definition of the behavior contract.</param>
    private PipelineKind(string requestDescription, Type requestContract, Type handlerContract, Type behaviorContract)
    {
        RequestDescription = requestDescription;
        RequestContract = requestContract;
        HandlerContract = handlerContract;
        BehaviorContract = behaviorContract;
    }

    /// <summary>
    /// Gets the kind of requests that produce a single response.
    /// </summary>
    public static PipelineKind Response { get; } = new(
        "request type",
        typeof(IRequest<>),
        typeof(IRequestHandler<,>),
        typeof(IPipelineBehavior<,>));

    /// <summary>
    /// Gets the kind of requests that produce a stream of items.
    /// </summary>
    public static PipelineKind Stream { get; } = new(
        "stream request type",
        typeof(IStreamRequest<>),
        typeof(IStreamRequestHandler<,>),
        typeof(IStreamPipelineBehavior<,>));

    /// <summary>
    /// Gets every pipeline kind.
    /// </summary>
    public static IReadOnlyList<PipelineKind> All { get; } = [Response, Stream];

    /// <summary>
    /// Gets how messages describe a request type of this kind, such as "request type".
    /// </summary>
    public string RequestDescription { get; }

    /// <summary>
    /// Gets the generic definition of the request contract, which has one type parameter.
    /// </summary>
    public Type RequestContract { get; }

    /// <summary>
    /// Gets the generic definition of the handler contract, which has two type parameters.
    /// </summary>
    public Type HandlerContract { get; }

    /// <summary>
    /// Gets the generic definition of the behavior contract, which has two type parameters.
    /// </summary>
    public Type BehaviorContract { get; }
}
