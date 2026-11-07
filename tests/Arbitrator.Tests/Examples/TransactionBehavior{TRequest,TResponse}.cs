namespace Arbitrator.Tests.Examples;

/// <summary>
/// Handles transactional requests inside a transaction that commits only when the pipeline succeeds.
/// </summary>
/// <typeparam name="TRequest">A transactional request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="unitOfWork">The unit of work of the current scope.</param>
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ITransactional
{
    /// <inheritdoc/>
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestContinuation<TResponse> next,
        CancellationToken cancellationToken)
    {
        await using ITransaction transaction = await unitOfWork.BeginAsync(cancellationToken);
        TResponse response = await next(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }
}
