namespace Arbitrator.Tests.Examples;

/// <summary>
/// Deactivates a customer through the scoped repository and announces the deactivation.
/// </summary>
/// <param name="customers">The repository of the current scope.</param>
/// <param name="publisher">The publisher of the current scope.</param>
public sealed class DeactivateCustomerHandler(ICustomerRepository customers, IPublisher publisher)
    : IRequestHandler<DeactivateCustomer>
{
    /// <inheritdoc/>
    public async Task HandleAsync(DeactivateCustomer request, CancellationToken cancellationToken)
    {
        Customer customer = await customers.FindAsync(request.Id, cancellationToken)
            ?? throw new CustomerNotFoundException(request.Id);

        customer.Deactivate(request.Reason);
        await customers.SaveChangesAsync(cancellationToken);
        await publisher.PublishAsync(new CustomerDeactivated(customer.Id, request.Reason), cancellationToken);
    }
}
