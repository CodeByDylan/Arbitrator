using Arbitrator.Dispatch;

namespace Arbitrator;

/// <summary>
/// Handles one request type that produces no response.
/// </summary>
/// <typeparam name="TRequest">The exact request type this handler handles.</typeparam>
public interface IRequestHandler<TRequest> : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest
{
    /// <summary>
    /// Handles a request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">The token that signals the request should be abandoned.</param>
    /// <returns>A task that completes when the request has been handled.</returns>
    new Task HandleAsync(TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Handles a request and reports completion as <see cref="Unit.Value"/>.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">The token that signals the request should be abandoned.</param>
    /// <returns>A task that produces <see cref="Unit.Value"/> when the request has been handled.</returns>
    /// <exception cref="InvalidOperationException">The handler returned a null task.</exception>
    async Task<Unit> IRequestHandler<TRequest, Unit>.HandleAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        Task handling = HandleAsync(request, cancellationToken)
            ?? throw DispatchErrors.NullHandlerTask(GetType(), typeof(TRequest));

        await handling.ConfigureAwait(false);
        return Unit.Value;
    }
}
