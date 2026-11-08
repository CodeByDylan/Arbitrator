namespace Arbitrator.Tests.Examples;

/// <summary>
/// Streams the customers whose name starts with a prefix.
/// </summary>
/// <param name="NamePrefix">The start of the customer names to find.</param>
public sealed record SearchCustomers(string NamePrefix) : IStreamRequest<CustomerDto>;
