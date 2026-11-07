namespace Arbitrator.Tests.Examples;

/// <summary>
/// Deactivates a customer through the scoped repository.
/// </summary>
/// <param name="customers">The repository of the current scope.</param>
public sealed class DeactivateCustomerHandler(ICustomerRepository customers) : IRequestHandler<DeactivateCustomer>
{
    /// <inheritdoc/>
    public async Task HandleAsync(DeactivateCustomer request, CancellationToken cancellationToken)
    {
        Customer customer = await customers.FindAsync(request.Id, cancellationToken)
            ?? throw new CustomerNotFoundException(request.Id);

        customer.Deactivate(request.Reason);
        await customers.SaveChangesAsync(cancellationToken);
    }
}
