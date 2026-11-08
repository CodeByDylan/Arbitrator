using System.Runtime.CompilerServices;

namespace Arbitrator.Tests.Examples;

/// <summary>
/// Streams matching customers from the scoped repository as they are read.
/// </summary>
/// <param name="customers">The repository of the current scope.</param>
public sealed class SearchCustomersHandler(ICustomerRepository customers)
    : IStreamRequestHandler<SearchCustomers, CustomerDto>
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<CustomerDto> HandleAsync(
        SearchCustomers request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (Customer customer in customers.SearchAsync(request.NamePrefix, cancellationToken))
        {
            yield return new CustomerDto(customer.Id, customer.Name, customer.IsActive);
        }
    }
}
