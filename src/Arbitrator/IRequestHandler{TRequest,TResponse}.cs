namespace Arbitrator;

/// <summary>
/// Handles one request type and produces its response.
/// </summary>
/// <typeparam name="TRequest">The exact request type this handler handles.</typeparam>
/// <typeparam name="TResponse">The type of the response the request produces.</typeparam>
public interface IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles a request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">The token that signals the request should be abandoned.</param>
    /// <returns>A task that produces the response.</returns>
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
