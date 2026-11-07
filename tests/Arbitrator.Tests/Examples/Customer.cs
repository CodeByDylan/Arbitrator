namespace Arbitrator.Tests.Examples;

/// <summary>
/// A customer that can be deactivated.
/// </summary>
/// <param name="id">The customer identifier.</param>
/// <param name="name">The customer name.</param>
public sealed class Customer(Guid id, string name)
{
    /// <summary>
    /// Gets the customer identifier.
    /// </summary>
    public Guid Id { get; } = id;

    /// <summary>
    /// Gets the customer name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets a value indicating whether the customer is active.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Gets the reason the customer was deactivated, if it was.
    /// </summary>
    public string? DeactivationReason { get; private set; }

    /// <summary>
    /// Deactivates the customer.
    /// </summary>
    /// <param name="reason">Why the customer is deactivated.</param>
    public void Deactivate(string reason)
    {
        IsActive = false;
        DeactivationReason = reason;
    }
}
