using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Pipeline;

/// <summary>
/// Specifies which behaviors wrap a request and in what order.
/// </summary>
public sealed class PipelineOrderingTests
{
    [Test]
    public async Task First_registered_behavior_is_the_outermost()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>))
            .AddOpenBehavior(typeof(SecondBehavior<,>))
            .AddHandler<PingHandler>());

        await host.Sender.SendAsync(new Ping("hello"));

        await Expect.EntriesAsync(
            host.Journal,
            "First.before",
            "Second.before",
            "handler",
            "Second.after",
            "First.after");
    }

    [Test]
    public async Task Open_and_closed_behaviors_follow_one_registration_order_across_registration_calls()
    {
        await using TestHost host = TestHost.CreateWithServices(services =>
        {
            services.AddArbitrator(arbitrator => arbitrator
                .AddOpenBehavior(typeof(FirstBehavior<,>))
                .AddBehavior<NotifyAuditBehavior>()
                .AddHandler<NotifyHandler>()
                .AddHandler<PingHandler>());
            services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(typeof(SecondBehavior<,>)));
        });

        await host.Sender.SendAsync(new Notify("hello"));
        await host.Sender.SendAsync(new Ping("hello"));

        await Expect.EntriesAsync(
            host.Journal,
            "First.before",
            "Audit.before",
            "Second.before",
            "handler",
            "Second.after",
            "Audit.after",
            "First.after",
            "First.before",
            "Second.before",
            "handler",
            "Second.after",
            "First.after");
    }

    [Test]
    public async Task Constrained_open_behavior_applies_only_to_requests_that_satisfy_its_constraints()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(MarkedRequestBehavior<,>))
            .AddOpenBehavior(typeof(ValueResponseBehavior<,>))
            .AddHandler<PingHandler>()
            .AddHandler<MarkedPingHandler>()
            .AddHandler<CountHandler>());

        await host.Sender.SendAsync(new Ping("plain"));
        await host.Sender.SendAsync(new MarkedPing());
        await host.Sender.SendAsync(new Count());

        await Expect.EntriesAsync(host.Journal, "handler", "marked", "marked handler", "value", "count handler");
    }

    [Test]
    public async Task Order_of_handler_and_behavior_registrations_does_not_change_the_pipeline()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandler<PingHandler>()
            .AddOpenBehavior(typeof(FirstBehavior<,>))
            .AddOpenBehavior(typeof(SecondBehavior<,>)));

        await host.Sender.SendAsync(new Ping("hello"));

        await Expect.EntriesAsync(
            host.Journal,
            "First.before",
            "Second.before",
            "handler",
            "Second.after",
            "First.after");
    }

    public interface IMarked;

    public sealed record MarkedPing : IRequest<string>, IMarked;

    public sealed record Count : IRequest<int>;

    public sealed class NotifyAuditBehavior(Journal journal) : RecordingBehavior<Notify, Unit>(journal, "Audit");

    public sealed class MarkedRequestBehavior<TRequest, TResponse>(Journal journal)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>, IMarked
    {
        public Task<TResponse> HandleAsync(
            TRequest request,
            RequestContinuation<TResponse> next,
            CancellationToken cancellationToken)
        {
            journal.Record("marked");
            return next(cancellationToken);
        }
    }

    public sealed class ValueResponseBehavior<TRequest, TResponse>(Journal journal)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : struct, IComparable<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request,
            RequestContinuation<TResponse> next,
            CancellationToken cancellationToken)
        {
            journal.Record("value");
            return next(cancellationToken);
        }
    }

    public sealed class MarkedPingHandler(Journal journal) : IRequestHandler<MarkedPing, string>
    {
        public Task<string> HandleAsync(MarkedPing request, CancellationToken cancellationToken)
        {
            journal.Record("marked handler");
            return Task.FromResult("marked");
        }
    }

    public sealed class CountHandler(Journal journal) : IRequestHandler<Count, int>
    {
        public Task<int> HandleAsync(Count request, CancellationToken cancellationToken)
        {
            journal.Record("count handler");
            return Task.FromResult(1);
        }
    }
}
