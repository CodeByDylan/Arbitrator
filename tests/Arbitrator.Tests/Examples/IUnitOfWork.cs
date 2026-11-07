namespace Arbitrator.Tests.Examples;

/// <summary>
/// Starts transactions.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Begins a transaction.
    /// </summary>
    /// <param name="cancellationToken">The token that signals the transaction should not be started.</param>
    /// <returns>The transaction. Disposing it without committing rolls it back.</returns>
    Task<ITransaction> BeginAsync(CancellationToken cancellationToken);
}
