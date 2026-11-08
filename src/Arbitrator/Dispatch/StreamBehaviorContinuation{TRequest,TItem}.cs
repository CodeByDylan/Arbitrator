namespace Arbitrator.Dispatch;

/// <summary>
/// A continuation that runs the next behavior in a stream pipeline.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TItem">The item type.</typeparam>
internal sealed class StreamBehaviorContinuation<TRequest, TItem> : GuardedStreamContinuation<TRequest, TItem>
    where TRequest : IStreamRequest<TItem>
{
    private readonly IStreamPipelineBehavior<TRequest, TItem> _behavior;
    private readonly TRequest _request;
    private readonly StreamContinuation<TItem> _next;

    /// <summary>
    /// Creates a continuation that runs a behavior with its own continuation.
    /// </summary>
    /// <param name="ownerType">The behavior that receives this continuation.</param>
    /// <param name="behavior">The behavior to run.</param>
    /// <param name="request">The request.</param>
    /// <param name="next">The continuation handed to <paramref name="behavior"/>.</param>
    public StreamBehaviorContinuation(
        Type ownerType,
        IStreamPipelineBehavior<TRequest, TItem> behavior,
        TRequest request,
        StreamContinuation<TItem> next)
        : base(ownerType)
    {
        _behavior = behavior;
        _request = request;
        _next = next;
    }

    /// <inheritdoc/>
    protected override IAsyncEnumerable<TItem> OpenNext(CancellationToken cancellationToken)
    {
        return ComponentStreams.FromBehavior(_behavior.HandleAsync(_request, _next, cancellationToken), _behavior);
    }
}
