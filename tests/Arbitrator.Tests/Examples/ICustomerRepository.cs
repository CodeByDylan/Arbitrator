namespace Arbitrator.Tests.Examples;

/// <summary>
/// Loads and saves customers within one unit of work.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>
    /// Finds a customer.
    /// </summary>
    /// <param name="id">The customer identifier.</param>
    /// <param name="cancellationToken">The token that signals the lookup should be abandoned.</param>
    /// <returns>The customer, or <see langword="null"/> when it does not exist.</returns>
    Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the changes made to loaded customers.
    /// </summary>
    /// <param name="cancellationToken">The token that signals the save should be abandoned.</param>
    /// <returns>A task that completes when the changes are saved.</returns>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
