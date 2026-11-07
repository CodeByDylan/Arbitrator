using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.NoResponse;

/// <summary>
/// Specifies dispatch of requests that produce no response.
/// </summary>
public sealed class NoResponseDispatchTests
{
    [Test]
    public async Task Send_returns_a_plain_task_that_completes_after_the_handler_ran_once()
    {
        await using var host = TestHost.Create(arbitrator => arbitrator.AddHandler<NotifyHandler>());

        Type resultType = StaticType.OfResult(() => host.Sender.SendAsync(new Notify("hello")));

        await host.Sender.SendAsync(new Notify("hello"));

        await Assert.That(resultType).IsEqualTo(typeof(Task));
        await Expect.EntriesAsync(host.Journal, "handler");
    }

    [Test]
    public async Task Behaviors_observe_unit_as_the_response_type_and_closed_behaviors_name_it()
    {
        await using var host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(ResponseTypeBehavior<,>))
            .AddBehavior<NotifyAuditBehavior>()
            .AddHandler<NotifyHandler>());

        await host.Sender.SendAsync(new Notify("hello"));

        await Expect.EntriesAsync(host.Journal, "response:Unit", "audit", "handler");
    }

    [Test]
    public async Task Handler_without_response_reports_unit_through_the_handler_contract()
    {
        var journal = new Journal();
        IRequestHandler<Notify, Unit> handler = new NotifyHandler(journal);

        Unit result = await handler.HandleAsync(new Notify("hello"), CancellationToken.None);

        await Assert.That(result).IsEqualTo(Unit.Value);
        await Expect.EntriesAsync(journal, "handler");
    }

    [Test]
    public async Task Handler_without_response_propagates_its_exception_through_the_handler_contract()
    {
        var failure = new InvalidOperationException("boom");
        IRequestHandler<Notify, Unit> handler = new FailingNotifyHandler(failure);

        InvalidOperationException thrown = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => handler.HandleAsync(new Notify("hello"), CancellationToken.None));

        await Assert.That(thrown).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task Behavior_can_complete_a_request_without_response_without_invoking_the_handler()
    {
        await using var host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<NotifySuppressingBehavior>()
            .AddHandler<NotifyHandler>());

        await host.Sender.SendAsync(new Notify("hello"));

        await Expect.EntriesAsync(host.Journal, "suppressed");
    }

    public sealed class ResponseTypeBehavior<TRequest, TResponse>(Journal journal)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request,
            RequestContinuation<TResponse> next,
            CancellationToken cancellationToken)
        {
            journal.Record($"response:{typeof(TResponse).Name}");
            return next(cancellationToken);
        }
    }

    public sealed class NotifyAuditBehavior(Journal journal) : IPipelineBehavior<Notify, Unit>
    {
        public Task<Unit> HandleAsync(
            Notify request,
            RequestContinuation<Unit> next,
            CancellationToken cancellationToken)
        {
            journal.Record("audit");
            return next(cancellationToken);
        }
    }

    public sealed class NotifySuppressingBehavior(Journal journal) : IPipelineBehavior<Notify, Unit>
    {
        public Task<Unit> HandleAsync(
            Notify request,
            RequestContinuation<Unit> next,
            CancellationToken cancellationToken)
        {
            journal.Record("suppressed");
            return Task.FromResult(Unit.Value);
        }
    }

    public sealed class FailingNotifyHandler(Exception failure) : IRequestHandler<Notify>
    {
        public async Task HandleAsync(Notify request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            throw failure;
        }
    }
}
