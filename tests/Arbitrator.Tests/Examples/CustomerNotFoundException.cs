namespace Arbitrator.Tests.Examples;

/// <summary>
/// The exception thrown when a customer does not exist.
/// </summary>
public sealed class CustomerNotFoundException : Exception
{
    /// <summary>
    /// Creates an exception for a missing customer.
    /// </summary>
    /// <param name="customerId">The identifier of the missing customer.</param>
    public CustomerNotFoundException(Guid customerId)
        : base($"Customer '{customerId}' does not exist.")
    {
        CustomerId = customerId;
    }

    /// <summary>
    /// Gets the identifier of the missing customer.
    /// </summary>
    public Guid CustomerId { get; }
}
