namespace Arbitrator.Tests.Examples;

/// <summary>
/// A transaction that is rolled back when it is disposed without being committed.
/// </summary>
public interface ITransaction : IAsyncDisposable
{
    /// <summary>
    /// Commits the transaction.
    /// </summary>
    /// <param name="cancellationToken">The token that signals the commit should be abandoned.</param>
    /// <returns>A task that completes when the transaction is committed.</returns>
    Task CommitAsync(CancellationToken cancellationToken);
}
