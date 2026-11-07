using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Examples;

/// <summary>
/// A transaction that records whether it was committed or rolled back.
/// </summary>
/// <param name="journal">The journal of the test.</param>
public sealed class InMemoryTransaction(Journal journal) : ITransaction
{
    private bool _isCommitted;

    /// <inheritdoc/>
    public Task CommitAsync(CancellationToken cancellationToken)
    {
        _isCommitted = true;
        journal.Record("committed");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Rolls the transaction back unless it was committed.
    /// </summary>
    /// <returns>A task that completes when the transaction has ended.</returns>
    public ValueTask DisposeAsync()
    {
        if (!_isCommitted)
        {
            journal.Record("rolled back");
        }

        return ValueTask.CompletedTask;
    }
}
