using System.Runtime.CompilerServices;
using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Examples;

/// <summary>
/// A customer repository holding one known customer, which records saves in the journal.
/// </summary>
/// <param name="journal">The journal of the test.</param>
public sealed class InMemoryCustomerRepository(Journal journal) : ICustomerRepository
{
    private readonly Customer _knownCustomer = new(KnownCustomerId, "Ada Lovelace");

    /// <summary>
    /// Gets the identifier of the customer every repository instance contains.
    /// </summary>
    public static Guid KnownCustomerId { get; } = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");

    /// <inheritdoc/>
    public Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(id == KnownCustomerId ? _knownCustomer : null);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Customer> SearchAsync(
        string namePrefix,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_knownCustomer.Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
        {
            yield return _knownCustomer;
        }
    }

    /// <inheritdoc/>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        journal.Record("saved");
        return Task.CompletedTask;
    }
}
