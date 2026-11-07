namespace Arbitrator.Tests.Examples;

/// <summary>
/// Gets a customer through the scoped repository.
/// </summary>
/// <param name="customers">The repository of the current scope.</param>
public sealed class GetCustomerHandler(ICustomerRepository customers) : IRequestHandler<GetCustomer, CustomerDto?>
{
    /// <inheritdoc/>
    public async Task<CustomerDto?> HandleAsync(GetCustomer request, CancellationToken cancellationToken)
    {
        Customer? customer = await customers.FindAsync(request.Id, cancellationToken);
        return customer is null ? null : new CustomerDto(customer.Id, customer.Name, customer.IsActive);
    }
}
