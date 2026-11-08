namespace Arbitrator.Dispatch;

/// <summary>
/// A pipeline continuation that runs the next component at most once.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResult">What the next component produces: a task or a stream.</typeparam>
internal abstract class GuardedContinuation<TRequest, TResult>
{
    private int _invoked;

    /// <summary>
    /// Creates a continuation that is handed to one behavior.
    /// </summary>
    /// <param name="ownerType">The behavior that receives this continuation, named when it is misused.</param>
    protected GuardedContinuation(Type ownerType)
    {
        OwnerType = ownerType;
    }

    /// <summary>
    /// Gets the behavior that receives this continuation.
    /// </summary>
    protected Type OwnerType { get; }

    /// <summary>
    /// Runs the next component. Matches the continuation delegate that the owning behavior receives.
    /// </summary>
    /// <param name="cancellationToken">The token passed to the next component.</param>
    /// <returns>What the next component produces.</returns>
    /// <exception cref="InvalidOperationException">The continuation has already been invoked.</exception>
    public TResult InvokeAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _invoked, 1) == 1)
        {
            throw DispatchErrors.ContinuationInvokedTwice(OwnerType, typeof(TRequest));
        }

        return InvokeNextAsync(cancellationToken);
    }

    /// <summary>
    /// Runs the next component.
    /// </summary>
    /// <param name="cancellationToken">The token passed to the next component.</param>
    /// <returns>What the next component produces.</returns>
    protected abstract TResult InvokeNextAsync(CancellationToken cancellationToken);
}
