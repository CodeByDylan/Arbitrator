namespace Arbitrator.Dispatch;

/// <summary>
/// A pipeline continuation that runs the next component at most once.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal abstract class GuardedContinuation<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly Type _ownerType;
    private int _invoked;

    /// <summary>
    /// Creates a continuation that is handed to one behavior.
    /// </summary>
    /// <param name="ownerType">The behavior that receives this continuation, named when it is misused.</param>
    protected GuardedContinuation(Type ownerType)
    {
        _ownerType = ownerType;
    }

    /// <summary>
    /// Runs the next component. Matches <see cref="RequestContinuation{TResponse}"/>.
    /// </summary>
    /// <param name="cancellationToken">The token passed to the next component.</param>
    /// <returns>A task that produces the response of the next component.</returns>
    /// <exception cref="InvalidOperationException">The continuation has already been invoked.</exception>
    public Task<TResponse> InvokeAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _invoked, 1) == 1)
        {
            throw DispatchErrors.ContinuationInvokedTwice(_ownerType, typeof(TRequest));
        }

        return InvokeNextAsync(cancellationToken);
    }

    /// <summary>
    /// Runs the next component.
    /// </summary>
    /// <param name="cancellationToken">The token passed to the next component.</param>
    /// <returns>A task that produces the response of the next component.</returns>
    protected abstract Task<TResponse> InvokeNextAsync(CancellationToken cancellationToken);
}
