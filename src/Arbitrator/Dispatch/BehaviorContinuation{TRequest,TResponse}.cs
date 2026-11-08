namespace Arbitrator.Dispatch;

/// <summary>
/// A continuation that runs the next behavior in the pipeline.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class BehaviorContinuation<TRequest, TResponse> : GuardedContinuation<TRequest, Task<TResponse>>
    where TRequest : IRequest<TResponse>
{
    private readonly IPipelineBehavior<TRequest, TResponse> _behavior;
    private readonly TRequest _request;
    private readonly RequestContinuation<TResponse> _next;

    /// <summary>
    /// Creates a continuation that runs a behavior with its own continuation.
    /// </summary>
    /// <param name="ownerType">The behavior that receives this continuation.</param>
    /// <param name="behavior">The behavior to run.</param>
    /// <param name="request">The request.</param>
    /// <param name="next">The continuation handed to <paramref name="behavior"/>.</param>
    public BehaviorContinuation(
        Type ownerType,
        IPipelineBehavior<TRequest, TResponse> behavior,
        TRequest request,
        RequestContinuation<TResponse> next)
        : base(ownerType)
    {
        _behavior = behavior;
        _request = request;
        _next = next;
    }

    /// <inheritdoc/>
    protected override Task<TResponse> InvokeNextAsync(CancellationToken cancellationToken)
    {
        return ComponentTasks.FromBehavior(_behavior.HandleAsync(_request, _next, cancellationToken), _behavior);
    }
}
