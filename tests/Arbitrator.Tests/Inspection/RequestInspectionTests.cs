using System.Runtime.CompilerServices;
using Arbitrator.Tests.Streaming;
using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;

namespace Arbitrator.Tests.Inspection;

/// <summary>
/// Specifies how <see cref="ArbitratorRegistrations.Requests"/> describes request types, their handlers, and the
/// behaviors that run for them.
/// </summary>
public sealed class RequestInspectionTests
{
    [Test]
    public async Task Request_entry_describes_the_request_its_response_and_its_handler()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<PingHandler>());

        RequestRegistration request = host.Registrations.Requests.Single();

        await Assert.That(request.RequestType).IsEqualTo(typeof(Ping));
        await Assert.That(request.ResponseType).IsEqualTo(typeof(Pong));
        await Assert.That(request.IsStream).IsFalse();
        await Assert.That(request.HandlerType).IsEqualTo(typeof(PingHandler));
        await Assert.That(request.BehaviorTypes).IsEmpty();
    }

    [Test]
    public async Task Request_without_a_response_reports_unit_as_its_response_type()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<NotifyHandler>());

        RequestRegistration request = host.Registrations.Requests.Single();

        await Assert.That(request.RequestType).IsEqualTo(typeof(Notify));
        await Assert.That(request.ResponseType).IsEqualTo(typeof(Unit));
    }

    [Test]
    public async Task Stream_request_entry_reports_its_item_type()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<CountdownHandler>());

        RequestRegistration request = host.Registrations.Requests.Single();

        await Assert.That(request.RequestType).IsEqualTo(typeof(Countdown));
        await Assert.That(request.ResponseType).IsEqualTo(typeof(Tick));
        await Assert.That(request.IsStream).IsTrue();
        await Assert.That(request.HandlerType).IsEqualTo(typeof(CountdownHandler));
    }

    [Test]
    public async Task Struct_request_entry_describes_it_like_any_other_request()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>))
            .AddHandler<MeasureHandler>());

        RequestRegistration request = host.Registrations.Requests.Single();

        await Assert.That(request.RequestType).IsEqualTo(typeof(Measure));
        await Assert.That(request.ResponseType).IsEqualTo(typeof(int));
        await Assert.That(request.HandlerType).IsEqualTo(typeof(MeasureHandler));
        await Assert.That(request.BehaviorTypes).IsEquivalentTo([typeof(FirstBehavior<Measure, int>)]);
    }

    [Test]
    public async Task Handler_with_several_contracts_has_one_entry_per_request_type()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<PingAndCountdownHandler>());

        IReadOnlyList<RequestRegistration> requests = host.Registrations.Requests;

        await Assert.That(requests.Select(request => request.RequestType)).IsEquivalentTo(
            [typeof(Countdown), typeof(Ping)]);
        await Assert.That(requests.All(request => request.HandlerType == typeof(PingAndCountdownHandler))).IsTrue();
    }

    [Test]
    public async Task Behavior_types_are_the_behaviors_that_run_outermost_first()
    {
        await using TestHost host = TestHost.CreateWithServices(services => services
            .AddArbitrator(arbitrator => arbitrator
                .AddOpenBehavior(typeof(FirstBehavior<,>))
                .AddOpenBehavior(typeof(FirstStreamBehavior<,>))
                .AddBehavior<PingAuditBehavior>())
            .AddArbitrator(arbitrator => arbitrator
                .AddOpenBehavior(typeof(ValidatedOnlyBehavior<,>))
                .AddOpenBehavior(typeof(StreamPipelineTests.ValueItemBehavior<,>))
                .AddOpenBehavior(typeof(SecondBehavior<,>))
                .AddHandler<PingHandler>()
                .AddHandler<CountdownHandler>()));

        IReadOnlyList<RequestRegistration> requests = host.Registrations.Requests;
        await host.Sender.SendAsync(new Ping("hello"));

        await Assert.That(requests.Single(request => request.RequestType == typeof(Ping)).BehaviorTypes)
            .IsEquivalentTo(
                [typeof(FirstBehavior<Ping, Pong>), typeof(PingAuditBehavior), typeof(SecondBehavior<Ping, Pong>)],
                CollectionOrdering.Matching);
        await Assert.That(requests.Single(request => request.RequestType == typeof(Countdown)).BehaviorTypes)
            .IsEquivalentTo([typeof(FirstStreamBehavior<Countdown, Tick>)]);
        await Expect.EntriesAsync(
            host.Journal,
            "First.before",
            "audit",
            "Second.before",
            "handler",
            "Second.after",
            "First.after");
    }

    [Test]
    public async Task Requests_are_sorted_by_ordinal_full_type_name()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandler<PingHandler>()
            .AddHandler<NotifyHandler>()
            .AddHandler<CountdownHandler>());

        IEnumerable<Type> requestTypes = host.Registrations.Requests.Select(request => request.RequestType);

        await Assert.That(requestTypes).IsEquivalentTo(
            [typeof(Countdown), typeof(Notify), typeof(Ping)],
            CollectionOrdering.Matching);
    }

    public readonly record struct Measure(int Length) : IRequest<int>;

    public sealed class MeasureHandler : IRequestHandler<Measure, int>
    {
        public Task<int> HandleAsync(Measure request, CancellationToken cancellationToken) =>
            Task.FromResult(request.Length);
    }

    public interface IValidated;

    public sealed class ValidatedOnlyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>, IValidated
    {
        public Task<TResponse> HandleAsync(
            TRequest request,
            RequestContinuation<TResponse> next,
            CancellationToken cancellationToken) => next(cancellationToken);
    }

    public sealed class PingAuditBehavior(Journal journal) : IPipelineBehavior<Ping, Pong>
    {
        public Task<Pong> HandleAsync(Ping request, RequestContinuation<Pong> next, CancellationToken cancellationToken)
        {
            journal.Record("audit");
            return next(cancellationToken);
        }
    }

    public sealed class PingAndCountdownHandler
        : IRequestHandler<Ping, Pong>, IStreamRequestHandler<Countdown, Tick>
    {
        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken) =>
            Task.FromResult(new Pong(request.Message));

        public async IAsyncEnumerable<Tick> HandleAsync(
            Countdown request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new Tick(request.From);
        }
    }
}
