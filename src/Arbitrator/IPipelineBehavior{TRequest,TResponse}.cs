using System.Diagnostics.CodeAnalysis;

namespace Arbitrator;

/// <summary>
/// Wraps the handling of a request to add cross-cutting behavior before and after the remainder of the pipeline.
/// </summary>
/// <remarks>
/// Behaviors run in registration order: the first registered behavior is the outermost. A behavior may inspect the
/// request and response, substitute the cancellation token passed to <c>next</c>, or return a response without
/// invoking <c>next</c>.
/// </remarks>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The type of the response the request produces.</typeparam>
public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles a request by optionally invoking the remainder of the pipeline.
    /// </summary>
    /// <param name="request">The request being handled.</param>
    /// <param name="next">The remainder of the pipeline. It may be invoked at most once.</param>
    /// <param name="cancellationToken">The token that signals the request should be abandoned.</param>
    /// <returns>A task that produces the response.</returns>
    [SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "Named next as in ASP.NET Core middleware; Visual Basic callers can still use it.")]
    Task<TResponse> HandleAsync(
        TRequest request,
        RequestContinuation<TResponse> next,
        CancellationToken cancellationToken);
}
