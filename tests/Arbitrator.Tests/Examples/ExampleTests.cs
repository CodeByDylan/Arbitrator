using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arbitrator.Tests.Examples;

/// <summary>
/// Exercises the examples of the design document: logging, validation, and transaction behaviors around handlers
/// that use scoped dependencies.
/// </summary>
public sealed class ExampleTests
{
    private static readonly Guid UnknownCustomerId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Test]
    public async Task Get_customer_returns_the_customer_from_the_scoped_repository()
    {
        await using TestHost host = CreateHost();

        var request = new GetCustomer(InMemoryCustomerRepository.KnownCustomerId);

        CustomerDto? customer = await host.Sender.SendAsync(request);

        await Assert.That(customer).IsEqualTo(
            new CustomerDto(InMemoryCustomerRepository.KnownCustomerId, "Ada Lovelace", IsActive: true));
    }

    [Test]
    public async Task Get_customer_returns_null_for_an_unknown_customer()
    {
        await using TestHost host = CreateHost();

        CustomerDto? customer = await host.Sender.SendAsync(new GetCustomer(UnknownCustomerId));

        await Assert.That(customer).IsNull();
    }

    [Test]
    public async Task Valid_deactivation_is_saved_and_announced_inside_a_committed_transaction()
    {
        await using TestHost host = CreateHost();

        await host.Sender.SendAsync(new DeactivateCustomer(InMemoryCustomerRepository.KnownCustomerId, "Moved away"));

        await Expect.EntriesAsync(
            host.Journal,
            "transaction begun",
            "saved",
            "audit recorded",
            "deactivation email queued",
            "committed");
    }

    [Test]
    public async Task Invalid_deactivation_is_rejected_before_a_transaction_begins()
    {
        await using TestHost host = CreateHost();

        RequestValidationException exception = await Expect.ThrowsExactlyAsync<RequestValidationException>(
            () => host.Sender.SendAsync(new DeactivateCustomer(InMemoryCustomerRepository.KnownCustomerId, " ")));

        await Assert.That(exception.Errors).IsEquivalentTo(["Reason is required."]);
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Failed_deactivation_rolls_the_transaction_back()
    {
        await using TestHost host = CreateHost();

        CustomerNotFoundException exception = await Expect.ThrowsExactlyAsync<CustomerNotFoundException>(
            () => host.Sender.SendAsync(new DeactivateCustomer(UnknownCustomerId, "Moved away")));

        await Assert.That(exception.CustomerId).IsEqualTo(UnknownCustomerId);
        await Expect.EntriesAsync(host.Journal, "transaction begun", "rolled back");
    }

    [Test]
    public async Task Send_infers_the_response_type_from_the_request()
    {
        await using TestHost host = CreateHost();
        var getCustomer = new GetCustomer(InMemoryCustomerRepository.KnownCustomerId);
        IRequest<CustomerDto?> getCustomerThroughInterface = getCustomer;
        IRequest deactivateThroughInterface = new DeactivateCustomer(UnknownCustomerId, "Moved away");

        Type withResponseType = StaticType.OfResult(() => host.Sender.SendAsync(getCustomer));
        Type withoutResponseType = StaticType.OfResult(
            () => host.Sender.SendAsync(new DeactivateCustomer(UnknownCustomerId, "x")));

        CustomerDto? concrete = await host.Sender.SendAsync(getCustomer);
        CustomerDto? throughInterface = await host.Sender.SendAsync(getCustomerThroughInterface);

        await Assert.That(withResponseType).IsEqualTo(typeof(Task<CustomerDto>));
        await Assert.That(withoutResponseType).IsEqualTo(typeof(Task));
        await Assert.That(concrete).IsEqualTo(throughInterface);
        await Assert.ThrowsExactlyAsync<CustomerNotFoundException>(
            () => host.Sender.SendAsync(deactivateThroughInterface));
    }

    [Test]
    public async Task Search_streams_matching_customers_from_the_scoped_repository()
    {
        await using TestHost host = CreateHost();

        List<CustomerDto> matching = [];
        await foreach (CustomerDto customer in host.Sender.StreamAsync(new SearchCustomers("ada")))
        {
            matching.Add(customer);
        }

        List<CustomerDto> none = await Streams.ReadAllAsync(host.Sender.StreamAsync(new SearchCustomers("Grace")));

        await Assert.That(matching).IsEquivalentTo(
            [new CustomerDto(InMemoryCustomerRepository.KnownCustomerId, "Ada Lovelace", IsActive: true)]);
        await Assert.That(none).IsEmpty();
    }

    private static TestHost CreateHost()
    {
        return TestHost.CreateWithServices(services => services
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddScoped<ICustomerRepository, InMemoryCustomerRepository>()
            .AddScoped<IUnitOfWork, InMemoryUnitOfWork>()
            .AddArbitrator(arbitrator => arbitrator
                .AddOpenBehavior(typeof(LoggingBehavior<,>))
                .AddOpenBehavior(typeof(ValidationBehavior<,>))
                .AddOpenBehavior(typeof(TransactionBehavior<,>))
                .AddOpenBehavior(typeof(StreamLoggingBehavior<,>))
                .AddHandler<GetCustomerHandler>()
                .AddHandler<SearchCustomersHandler>()
                .AddHandler<DeactivateCustomerHandler>()
                .AddNotificationHandler<DeactivationAuditRecorder>()
                .AddNotificationHandler<DeactivationEmailSender>()));
    }
}
