using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Pipeline;

/// <summary>
/// Specifies short-circuiting and the rules a behavior must follow when it invokes its continuation.
/// </summary>
public sealed class PipelineControlTests
{
    [Test]
    public async Task Short_circuiting_behavior_skips_inner_behaviors_and_the_handler()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<CachedPingBehavior>()
            .AddOpenBehavior(typeof(SecondBehavior<,>))
            .AddHandler<PingHandler>());

        Pong response = await host.Sender.SendAsync(new Ping("hello"));

        await Assert.That(response).IsSameReferenceAs(CachedPingBehavior.CachedResponse);
        await Expect.EntriesAsync(host.Journal, "cache hit");
    }

    [Test]
    public async Task Second_continuation_call_is_rejected_and_the_handler_runs_once()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<RepeatingBehavior>()
            .AddHandler<PingHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => host.Sender.SendAsync(new Ping("hello")));

        await Assert.That(exception.Message).Contains(typeof(RepeatingBehavior).Name);
        await Assert.That(exception.Message).Contains("more than once");
        await Expect.EntriesAsync(host.Journal, "handler");
    }

    [Test]
    public async Task Racing_continuation_calls_run_the_remainder_of_the_pipeline_once()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<RacingBehavior>()
            .AddHandler<PingHandler>());

        Pong response = await host.Sender.SendAsync(new Ping("hello"));

        await Assert.That(response.Message).IsEqualTo("hello");
        await Assert.That(host.Journal.Entries).Contains("rejected");
        await Assert.That(host.Journal.Entries.Count(entry => entry == "handler")).IsEqualTo(1);
    }

    [Test]
    public async Task Handler_returning_a_null_task_is_reported_by_name()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<NullTaskHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => host.Sender.SendAsync(new Ping("hello")));

        await Assert.That(exception.Message).Contains($"Handler '{Names.Of<NullTaskHandler>()}'");
        await Assert.That(exception.Message).Contains("null Task");
    }

    [Test]
    public async Task Handler_without_response_returning_a_null_task_is_reported_by_name()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<NullTaskNotifyHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => host.Sender.SendAsync(new Notify("hello")));

        await Assert.That(exception.Message).Contains($"Handler '{Names.Of<NullTaskNotifyHandler>()}'");
        await Assert.That(exception.Message).Contains($"while handling '{Names.Of<Notify>()}'");
    }

    [Test]
    public async Task Outermost_behavior_returning_a_null_task_is_reported_by_name()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<NullTaskBehavior>()
            .AddHandler<PingHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => host.Sender.SendAsync(new Ping("hello")));

        await Assert.That(exception.Message).Contains($"Behavior '{Names.Of<NullTaskBehavior>()}'");
        await Expect.EntriesAsync(host.Journal);
    }

    [Test]
    public async Task Behavior_returning_a_null_task_is_reported_by_name()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>))
            .AddBehavior<NullTaskBehavior>()
            .AddHandler<PingHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => host.Sender.SendAsync(new Ping("hello")));

        await Assert.That(exception.Message).Contains($"Behavior '{Names.Of<NullTaskBehavior>()}'");
    }

    public sealed class CachedPingBehavior(Journal journal) : IPipelineBehavior<Ping, Pong>
    {
        public static Pong CachedResponse { get; } = new("cached");

        public Task<Pong> HandleAsync(Ping request, RequestContinuation<Pong> next, CancellationToken cancellationToken)
        {
            journal.Record("cache hit");
            return Task.FromResult(CachedResponse);
        }
    }

    public sealed class RepeatingBehavior : IPipelineBehavior<Ping, Pong>
    {
        public async Task<Pong> HandleAsync(
            Ping request,
            RequestContinuation<Pong> next,
            CancellationToken cancellationToken)
        {
            await next(cancellationToken);
            return await next(cancellationToken);
        }
    }

    public sealed class RacingBehavior(Journal journal) : IPipelineBehavior<Ping, Pong>
    {
        private static readonly TimeSpan RaceStartTimeout = TimeSpan.FromSeconds(10);

        public async Task<Pong> HandleAsync(
            Ping request,
            RequestContinuation<Pong> next,
            CancellationToken cancellationToken)
        {
            using var startingLine = new Barrier(participantCount: 2);
            Task<Pong>[] attempts =
            [
                Task.Run(() => RaceAsync(startingLine, next, cancellationToken), cancellationToken),
                Task.Run(() => RaceAsync(startingLine, next, cancellationToken), cancellationToken),
            ];

            try
            {
                await Task.WhenAll(attempts);
            }
            catch (InvalidOperationException)
            {
                journal.Record("rejected");
            }

            return await attempts.Single(attempt => attempt.IsCompletedSuccessfully);
        }

        private static Task<Pong> RaceAsync(
            Barrier startingLine,
            RequestContinuation<Pong> next,
            CancellationToken cancellationToken)
        {
            startingLine.SignalAndWait(RaceStartTimeout, cancellationToken);
            return next(cancellationToken);
        }
    }

    public sealed class NullTaskHandler : IRequestHandler<Ping, Pong>
    {
        // Deliberately breaks the non-null Task contract.
        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken) => null!;
    }

    public sealed class NullTaskNotifyHandler : IRequestHandler<Notify>
    {
        // Deliberately breaks the non-null Task contract.
        public Task HandleAsync(Notify request, CancellationToken cancellationToken) => null!;
    }

    public sealed class NullTaskBehavior : IPipelineBehavior<Ping, Pong>
    {
        public Task<Pong> HandleAsync(Ping request, RequestContinuation<Pong> next, CancellationToken cancellationToken)
        {
            // Deliberately breaks the non-null Task contract.
            return null!;
        }
    }
}
