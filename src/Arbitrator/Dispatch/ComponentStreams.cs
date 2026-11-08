namespace Arbitrator.Dispatch;

/// <summary>
/// Checks the streams returned by stream handlers and behaviors before they flow through the pipeline.
/// </summary>
internal static class ComponentStreams
{
    /// <summary>
    /// Returns the stream a handler produced, or reports the handler when it produced none.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="stream">The stream returned by the handler.</param>
    /// <param name="handler">The handler that returned the stream.</param>
    /// <returns>The stream.</returns>
    /// <exception cref="InvalidOperationException">The handler returned <see langword="null"/>.</exception>
    public static IAsyncEnumerable<TItem> FromHandler<TRequest, TItem>(
        IAsyncEnumerable<TItem>? stream,
        IStreamRequestHandler<TRequest, TItem> handler)
        where TRequest : IStreamRequest<TItem>
    {
        return stream ?? throw DispatchErrors.NullHandlerStream(handler.GetType(), typeof(TRequest));
    }

    /// <summary>
    /// Returns the stream a behavior produced, or reports the behavior when it produced none.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="stream">The stream returned by the behavior.</param>
    /// <param name="behavior">The behavior that returned the stream.</param>
    /// <returns>The stream.</returns>
    /// <exception cref="InvalidOperationException">The behavior returned <see langword="null"/>.</exception>
    public static IAsyncEnumerable<TItem> FromBehavior<TRequest, TItem>(
        IAsyncEnumerable<TItem>? stream,
        IStreamPipelineBehavior<TRequest, TItem> behavior)
        where TRequest : IStreamRequest<TItem>
    {
        return stream ?? throw DispatchErrors.NullBehaviorStream(behavior.GetType(), typeof(TRequest));
    }
}
