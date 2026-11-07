using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Dispatch;

/// <summary>
/// Specifies how requests that produce a response reach exactly one handler.
/// </summary>
public sealed class TypedDispatchTests
{
    [Test]
    public async Task Send_returns_the_handlers_response_instance_for_the_callers_request_instance()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<PingHandler>());
        var request = new Ping("hello");

        Pong response = await host.Sender.SendAsync(request);

        await Assert.That(response).IsSameReferenceAs(host.Journal.Captured<Pong>().Single());
        await Assert.That(host.Journal.Captured<Ping>().Single()).IsSameReferenceAs(request);
    }

    [Test]
    public async Task Request_held_through_its_interface_is_dispatched_by_its_runtime_type()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<PingHandler>());
        IRequest<Pong> request = new Ping("hello");

        Pong response = await host.Sender.SendAsync(request);

        await Assert.That(response.Message).IsEqualTo("hello");
        await Expect.EntriesAsync(host.Journal, "handler");
    }

    [Test]
    public async Task Struct_request_round_trips_its_payload()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<SumHandler>());

        int sum = await host.Sender.SendAsync(new Sum(2, 3));

        await Assert.That(sum).IsEqualTo(5);
    }

    [Test]
    public async Task Struct_request_passes_through_behaviors()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>))
            .AddHandler<SumHandler>());

        int sum = await host.Sender.SendAsync(new Sum(2, 3));

        await Assert.That(sum).IsEqualTo(5);
        await Expect.EntriesAsync(host.Journal, "First.before", "First.after");
    }

    [Test]
    public async Task Class_implementing_two_handler_contracts_serves_both_with_a_new_instance_per_send()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<GreetingHandlers>());

        string greeting = await host.Sender.SendAsync(new Greet("Ada"));
        await host.Sender.SendAsync(new Wave("Ada"));

        IReadOnlyList<GreetingHandlers> instances = host.Journal.Captured<GreetingHandlers>();
        await Assert.That(greeting).IsEqualTo("Hello, Ada");
        await Expect.EntriesAsync(host.Journal, "greet", "wave");
        await Assert.That(instances[0]).IsNotSameReferenceAs(instances[1]);
    }

    [Test]
    public async Task Derived_request_type_is_not_dispatched_to_the_handler_of_its_base_type()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<CustomerQueryHandler>());

        ArbitratorConfigurationException exception = await Expect.ThrowsExactlyAsync<ArbitratorConfigurationException>(
            () => host.Sender.SendAsync(new VipCustomerQuery()));

        await Assert.That(exception.Message).Contains($"'{Names.Of<VipCustomerQuery>()}'");
        await Assert.That(exception.Message).Contains("base type");
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Missing_handler_message_names_the_request_and_the_handler_contract_to_implement()
    {
        await using TestHost host = TestHost.Create(_ => { });

        ArbitratorConfigurationException exception = await Expect.ThrowsExactlyAsync<ArbitratorConfigurationException>(
            () => host.Sender.SendAsync(new Ping("hello")));

        await Assert.That(exception.Message).Contains("'Arbitrator.Tests.Support.Ping'");
        await Assert.That(exception.Message)
            .Contains("IRequestHandler<Arbitrator.Tests.Support.Ping, Arbitrator.Tests.Support.Pong>");
    }

    [Test]
    public async Task Handler_registered_directly_on_the_service_collection_is_not_used()
    {
        await using TestHost host = TestHost.CreateWithServices(services =>
        {
            services.AddTransient<IRequestHandler<Ping, Pong>, PingHandler>();
            services.AddArbitrator(_ => { });
        });

        await Assert.ThrowsExactlyAsync<ArbitratorConfigurationException>(
            () => host.Sender.SendAsync(new Ping("hello")));
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    public readonly record struct Sum(int Left, int Right) : IRequest<int>;

    public sealed class SumHandler : IRequestHandler<Sum, int>
    {
        public Task<int> HandleAsync(Sum request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request.Left + request.Right);
        }
    }

    public sealed record Greet(string Name) : IRequest<string>;

    public sealed record Wave(string Name) : IRequest;

    public sealed class GreetingHandlers(Journal journal) : IRequestHandler<Greet, string>, IRequestHandler<Wave>
    {
        public Task<string> HandleAsync(Greet request, CancellationToken cancellationToken)
        {
            journal.Record("greet");
            journal.Capture(this);
            return Task.FromResult($"Hello, {request.Name}");
        }

        public Task HandleAsync(Wave request, CancellationToken cancellationToken)
        {
            journal.Record("wave");
            journal.Capture(this);
            return Task.CompletedTask;
        }
    }

    public record CustomerQuery : IRequest<string>;

    public sealed record VipCustomerQuery : CustomerQuery;

    public sealed class CustomerQueryHandler(Journal journal) : IRequestHandler<CustomerQuery, string>
    {
        public Task<string> HandleAsync(CustomerQuery request, CancellationToken cancellationToken)
        {
            journal.Record("handler");
            return Task.FromResult("customer");
        }
    }
}
