using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Cancellation;

/// <summary>
/// Specifies how the caller's cancellation token is checked once and otherwise forwarded.
/// </summary>
public sealed class CancellationTests
{
    [Test]
    public async Task Every_behavior_and_the_handler_receive_the_callers_token()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>))
            .AddOpenBehavior(typeof(SecondBehavior<,>))
            .AddHandler<PingHandler>());
        using var cancellation = new CancellationTokenSource();

        await host.Sender.SendAsync(new Ping("hello"), cancellation.Token);

        IEnumerable<CancellationToken> observedTokens = host.Journal.Captured<ObservedToken>()
            .Select(observed => observed.Token)
            .Concat(host.Journal.Captured<CancellationToken>());
        await Assert.That(observedTokens).All(token => token == cancellation.Token);
        await Assert.That(host.Journal.Captured<CancellationToken>()).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Substituted_token_reaches_inner_components_while_outer_components_keep_the_original()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>))
            .AddBehavior<TokenSubstitutingBehavior>()
            .AddOpenBehavior(typeof(SecondBehavior<,>))
            .AddHandler<PingHandler>());
        using var cancellation = new CancellationTokenSource();

        await host.Sender.SendAsync(new Ping("hello"), cancellation.Token);

        Dictionary<string, CancellationToken> tokensByObserver = host.Journal.Captured<ObservedToken>()
            .ToDictionary(observed => observed.Observer, observed => observed.Token);
        CancellationToken substitute = tokensByObserver["Substitute"];
        await Assert.That(substitute).IsNotEqualTo(cancellation.Token);
        await Assert.That(tokensByObserver["First"]).IsEqualTo(cancellation.Token);
        await Assert.That(tokensByObserver["Second"]).IsEqualTo(substitute);
        await Assert.That(host.Journal.Captured<CancellationToken>().Single()).IsEqualTo(substitute);
    }

    [Test]
    public async Task Already_canceled_token_cancels_the_send_before_anything_is_constructed()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>))
            .AddHandler<ConstructionTrackingHandler>());
        var canceled = new CancellationToken(canceled: true);

        Task<Pong> sending = host.Sender.SendAsync(new Ping("hello"), canceled);

        OperationCanceledException exception =
            await Expect.ThrowsExactlyAsync<OperationCanceledException>(() => sending);
        await Assert.That(sending.IsCanceled).IsTrue();
        await Assert.That(exception.CancellationToken).IsEqualTo(canceled);
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Already_canceled_token_cancels_a_send_without_response()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<NotifyHandler>());
        var canceled = new CancellationToken(canceled: true);

        Task sending = host.Sender.SendAsync(new Notify("hello"), canceled);

        OperationCanceledException exception =
            await Expect.ThrowsExactlyAsync<OperationCanceledException>(() => sending);
        await Assert.That(sending.IsCanceled).IsTrue();
        await Assert.That(exception.CancellationToken).IsEqualTo(canceled);
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Missing_handler_is_reported_even_when_the_token_is_already_canceled()
    {
        await using TestHost host = TestHost.Create(_ => { });

        await Expect.ThrowsExactlyAsync<ArbitratorConfigurationException>(
            () => host.Sender.SendAsync(new Ping("hello"), new CancellationToken(canceled: true)));
    }

    [Test]
    public async Task Cancellation_observed_by_the_handler_cancels_the_send_and_unwinds_behaviors()
    {
        await using TestHost host = TestHost.CreateWithServices(services => services
            .AddSingleton<Gate>()
            .AddArbitrator(arbitrator => arbitrator
                .AddBehavior<FinallyBehavior>()
                .AddHandler<WaitingHandler>()));
        using var cancellation = new CancellationTokenSource();
        Gate gate = host.Provider.GetRequiredService<Gate>();

        Task<string> sending = host.Sender.SendAsync(new Wait(), cancellation.Token);
        await gate.WaitUntilHandlerStartedAsync(sending);
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => sending);
        await Assert.That(sending.IsCanceled).IsTrue();
        await Expect.EntriesAsync(host.Journal, "finally");
    }

    [Test]
    public async Task Handler_that_ignores_cancellation_completes_with_its_response()
    {
        await using TestHost host = TestHost.CreateWithServices(services => services
            .AddSingleton<Gate>()
            .AddArbitrator(arbitrator => arbitrator.AddHandler<IgnoringHandler>()));
        using var cancellation = new CancellationTokenSource();
        Gate gate = host.Provider.GetRequiredService<Gate>();

        Task<string> sending = host.Sender.SendAsync(new Wait(), cancellation.Token);
        await gate.WaitUntilHandlerStartedAsync(sending);
        await cancellation.CancelAsync();
        gate.Release.SetResult();

        await Assert.That(await sending).IsEqualTo("completed");
    }

    public sealed record Wait : IRequest<string>;

    public sealed class Gate
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WaitUntilHandlerStartedAsync(Task sending)
        {
            Task first = await Task.WhenAny(Started.Task, sending);

            if (first == sending)
            {
                await sending;
                throw new InvalidOperationException("The send completed before its handler started.");
            }
        }
    }

    public sealed class ConstructionTrackingHandler : IRequestHandler<Ping, Pong>
    {
        public ConstructionTrackingHandler(Journal journal)
        {
            journal.Record("constructed");
        }

        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new Pong(request.Message));
        }
    }

    public sealed class TokenSubstitutingBehavior(Journal journal) : IPipelineBehavior<Ping, Pong>
    {
        public async Task<Pong> HandleAsync(
            Ping request,
            RequestContinuation<Pong> next,
            CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            journal.Capture(new ObservedToken("Substitute", linked.Token));
            return await next(linked.Token);
        }
    }

    public sealed class FinallyBehavior(Journal journal) : IPipelineBehavior<Wait, string>
    {
        public async Task<string> HandleAsync(
            Wait request,
            RequestContinuation<string> next,
            CancellationToken cancellationToken)
        {
            try
            {
                return await next(cancellationToken);
            }
            finally
            {
                journal.Record("finally");
            }
        }
    }

    public sealed class WaitingHandler(Gate gate) : IRequestHandler<Wait, string>
    {
        public async Task<string> HandleAsync(Wait request, CancellationToken cancellationToken)
        {
            gate.Started.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return "completed";
        }
    }

    public sealed class IgnoringHandler(Gate gate) : IRequestHandler<Wait, string>
    {
        public async Task<string> HandleAsync(Wait request, CancellationToken cancellationToken)
        {
            gate.Started.SetResult();
            await gate.Release.Task;
            return "completed";
        }
    }
}
