namespace Arbitrator.Dispatch;

/// <summary>
/// The innermost continuation of a stream pipeline, which runs the handler.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TItem">The item type.</typeparam>
internal sealed class StreamHandlerContinuation<TRequest, TItem> : GuardedStreamContinuation<TRequest, TItem>
    where TRequest : IStreamRequest<TItem>
{
    private readonly IStreamRequestHandler<TRequest, TItem> _handler;
    private readonly TRequest _request;

    /// <summary>
    /// Creates a continuation that runs a handler.
    /// </summary>
    /// <param name="ownerType">The innermost behavior, which receives this continuation.</param>
    /// <param name="handler">The handler to run.</param>
    /// <param name="request">The request.</param>
    public StreamHandlerContinuation(Type ownerType, IStreamRequestHandler<TRequest, TItem> handler, TRequest request)
        : base(ownerType)
    {
        _handler = handler;
        _request = request;
    }

    /// <inheritdoc/>
    protected override IAsyncEnumerable<TItem> OpenNext(CancellationToken cancellationToken)
    {
        return ComponentStreams.FromHandler(_handler.HandleAsync(_request, cancellationToken), _handler);
    }
}
