namespace Arbitrator.Dispatch;

/// <summary>
/// Runs the pipeline of one request type whose response type is known to the caller.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
internal abstract class RequestInvoker<TResponse> : RequestInvoker
{
    /// <summary>
    /// Resolves the behaviors and handler from a scope and runs the pipeline for a request.
    /// </summary>
    /// <param name="request">The request, whose runtime type matches this invoker's request type.</param>
    /// <param name="services">The scope's service provider.</param>
    /// <param name="cancellationToken">The token passed to the outermost component.</param>
    /// <returns>A task that produces the response of the pipeline.</returns>
    public abstract Task<TResponse> InvokeAsync(
        IRequest<TResponse> request,
        IServiceProvider services,
        CancellationToken cancellationToken);
}
