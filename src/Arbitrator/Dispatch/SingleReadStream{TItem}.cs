namespace Arbitrator.Dispatch;

/// <summary>
/// A stream that may be read once, handed to a behavior by its continuation.
/// </summary>
/// <typeparam name="TItem">The item type.</typeparam>
internal sealed class SingleReadStream<TItem> : IAsyncEnumerable<TItem>
{
    private readonly IAsyncEnumerable<TItem> _stream;
    private readonly Type _ownerType;
    private readonly Type _requestType;
    private int _read;

    /// <summary>
    /// Wraps the stream of the next component.
    /// </summary>
    /// <param name="stream">The stream of the next component.</param>
    /// <param name="ownerType">The behavior that reads the stream, named when it reads it twice.</param>
    /// <param name="requestType">The request type being handled.</param>
    public SingleReadStream(IAsyncEnumerable<TItem> stream, Type ownerType, Type requestType)
    {
        _stream = stream;
        _ownerType = ownerType;
        _requestType = requestType;
    }

    /// <summary>
    /// Starts the only read of the wrapped stream.
    /// </summary>
    /// <param name="cancellationToken">The token forwarded to the wrapped stream.</param>
    /// <returns>The enumerator of the wrapped stream.</returns>
    /// <exception cref="InvalidOperationException">The stream has already been read.</exception>
    public IAsyncEnumerator<TItem> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _read, 1) == 1)
        {
            throw DispatchErrors.ContinuationStreamReadTwice(_ownerType, _requestType);
        }

        return _stream.GetAsyncEnumerator(cancellationToken);
    }
}
