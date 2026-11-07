using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Examples;

/// <summary>
/// A unit of work whose transactions record their outcome in the journal.
/// </summary>
/// <param name="journal">The journal of the test.</param>
public sealed class InMemoryUnitOfWork(Journal journal) : IUnitOfWork
{
    /// <inheritdoc/>
    public Task<ITransaction> BeginAsync(CancellationToken cancellationToken)
    {
        journal.Record("transaction begun");
        return Task.FromResult<ITransaction>(new InMemoryTransaction(journal));
    }
}
