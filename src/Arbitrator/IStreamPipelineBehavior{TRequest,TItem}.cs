using System.Diagnostics.CodeAnalysis;

namespace Arbitrator;

/// <summary>
/// Wraps the read of a stream request to add cross-cutting behavior before, around, and after the items of the
/// remainder of the pipeline.
/// </summary>
/// <remarks>
/// Behaviors run in registration order: the first registered behavior is the outermost. A behavior may inspect the
/// request and the items, substitute the cancellation token passed to <c>next</c>, or produce items without invoking
/// <c>next</c>.
/// </remarks>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TItem">The type of the items the request produces.</typeparam>
public interface IStreamPipelineBehavior<TRequest, TItem>
    where TRequest : IStreamRequest<TItem>
{
    /// <summary>
    /// Handles a stream request by optionally reading the remainder of the pipeline.
    /// </summary>
    /// <param name="request">The request being handled.</param>
    /// <param name="next">
    /// The remainder of the pipeline. It may be invoked at most once, and the stream it returns may be read at most
    /// once.
    /// </param>
    /// <param name="cancellationToken">The token that signals the read should be abandoned.</param>
    /// <returns>The items of the request.</returns>
    [SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "Named next as in ASP.NET Core middleware; Visual Basic callers can still use it.")]
    IAsyncEnumerable<TItem> HandleAsync(
        TRequest request,
        StreamContinuation<TItem> next,
        CancellationToken cancellationToken);
}
