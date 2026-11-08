namespace Arbitrator.Dispatch;

/// <summary>
/// A stream pipeline continuation that runs the next component at most once and lets the owning behavior read the
/// resulting stream at most once.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TItem">The item type.</typeparam>
internal abstract class GuardedStreamContinuation<TRequest, TItem>
    : GuardedContinuation<TRequest, IAsyncEnumerable<TItem>>
{
    /// <summary>
    /// Creates a continuation that is handed to one behavior.
    /// </summary>
    /// <param name="ownerType">The behavior that receives this continuation, named when it is misused.</param>
    protected GuardedStreamContinuation(Type ownerType)
        : base(ownerType)
    {
    }

    /// <inheritdoc/>
    protected sealed override IAsyncEnumerable<TItem> InvokeNextAsync(CancellationToken cancellationToken)
    {
        return new SingleReadStream<TItem>(OpenNext(cancellationToken), OwnerType, typeof(TRequest));
    }

    /// <summary>
    /// Invokes the next component.
    /// </summary>
    /// <param name="cancellationToken">The token passed to the next component.</param>
    /// <returns>The stream of the next component, not yet read.</returns>
    protected abstract IAsyncEnumerable<TItem> OpenNext(CancellationToken cancellationToken);
}
