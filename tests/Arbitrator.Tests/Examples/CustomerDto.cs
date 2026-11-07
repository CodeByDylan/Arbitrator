namespace Arbitrator.Tests.Examples;

/// <summary>
/// The customer data returned to callers.
/// </summary>
/// <param name="Id">The customer identifier.</param>
/// <param name="Name">The customer name.</param>
/// <param name="IsActive">Whether the customer is active.</param>
public sealed record CustomerDto(Guid Id, string Name, bool IsActive);
