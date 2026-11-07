namespace Arbitrator.Tests.Examples;

/// <summary>
/// Deactivates a customer.
/// </summary>
/// <param name="Id">The customer identifier.</param>
/// <param name="Reason">Why the customer is deactivated.</param>
public sealed record DeactivateCustomer(Guid Id, string Reason) : IRequest, IValidatable, ITransactional
{
    /// <inheritdoc/>
    public IReadOnlyList<string> Validate() =>
        string.IsNullOrWhiteSpace(Reason) ? ["Reason is required."] : [];
}
