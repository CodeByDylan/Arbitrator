namespace Arbitrator.Tests.Examples;

/// <summary>
/// Gets a customer, or <see langword="null"/> when it does not exist.
/// </summary>
/// <param name="Id">The customer identifier.</param>
public sealed record GetCustomer(Guid Id) : IRequest<CustomerDto?>;
