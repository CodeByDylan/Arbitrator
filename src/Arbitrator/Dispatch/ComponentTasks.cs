namespace Arbitrator.Dispatch;

/// <summary>
/// Checks the tasks returned by handlers and behaviors before they flow through the pipeline.
/// </summary>
internal static class ComponentTasks
{
    /// <summary>
    /// Returns the task a handler produced, or reports the handler when it produced none.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="task">The task returned by the handler.</param>
    /// <param name="handler">The handler that returned the task.</param>
    /// <returns>The task.</returns>
    /// <exception cref="InvalidOperationException">The handler returned <see langword="null"/>.</exception>
    public static Task<TResponse> FromHandler<TRequest, TResponse>(
        Task<TResponse>? task,
        IRequestHandler<TRequest, TResponse> handler)
        where TRequest : IRequest<TResponse>
    {
        return task ?? throw DispatchErrors.NullHandlerTask(handler.GetType(), typeof(TRequest));
    }

    /// <summary>
    /// Returns the task a behavior produced, or reports the behavior when it produced none.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="task">The task returned by the behavior.</param>
    /// <param name="behavior">The behavior that returned the task.</param>
    /// <returns>The task.</returns>
    /// <exception cref="InvalidOperationException">The behavior returned <see langword="null"/>.</exception>
    public static Task<TResponse> FromBehavior<TRequest, TResponse>(
        Task<TResponse>? task,
        IPipelineBehavior<TRequest, TResponse> behavior)
        where TRequest : IRequest<TResponse>
    {
        return task ?? throw DispatchErrors.NullBehaviorTask(behavior.GetType(), typeof(TRequest));
    }
}
