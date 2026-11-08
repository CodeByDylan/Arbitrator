namespace Arbitrator.Dispatch;

/// <summary>
/// The innermost continuation of a pipeline, which runs the handler.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class HandlerContinuation<TRequest, TResponse> : GuardedContinuation<TRequest, Task<TResponse>>
    where TRequest : IRequest<TResponse>
{
    private readonly IRequestHandler<TRequest, TResponse> _handler;
    private readonly TRequest _request;

    /// <summary>
    /// Creates a continuation that runs a handler.
    /// </summary>
    /// <param name="ownerType">The innermost behavior, which receives this continuation.</param>
    /// <param name="handler">The handler to run.</param>
    /// <param name="request">The request.</param>
    public HandlerContinuation(Type ownerType, IRequestHandler<TRequest, TResponse> handler, TRequest request)
        : base(ownerType)
    {
        _handler = handler;
        _request = request;
    }

    /// <inheritdoc/>
    protected override Task<TResponse> InvokeNextAsync(CancellationToken cancellationToken)
    {
        return ComponentTasks.FromHandler(_handler.HandleAsync(_request, cancellationToken), _handler);
    }
}
