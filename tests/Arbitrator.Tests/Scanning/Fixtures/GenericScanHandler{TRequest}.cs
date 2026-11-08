namespace Arbitrator.Tests.Scanning.Fixtures;

/// <summary>
/// An open-generic handler definition, which scanning skips.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
public sealed class GenericScanHandler<TRequest> : IRequestHandler<TRequest>
    where TRequest : IRequest
{
    /// <inheritdoc/>
    public Task HandleAsync(TRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
}
