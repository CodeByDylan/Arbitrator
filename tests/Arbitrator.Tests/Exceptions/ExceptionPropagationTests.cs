using System.Reflection;
using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Exceptions;

/// <summary>
/// Specifies that exceptions pass through Arbitrator unwrapped and that every failure is reported through the task.
/// </summary>
public sealed class ExceptionPropagationTests
{
    [Test]
    public async Task Handler_exception_reaches_the_caller_as_the_same_instance_with_its_stack_trace()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>))
            .AddHandler<ThrowingHandler>());

        ExpectedFailureException thrown = await Expect.ThrowsExactlyAsync<ExpectedFailureException>(
            () => host.Sender.SendAsync(new Ping("hello")));

        await Assert.That(thrown).IsSameReferenceAs(host.Journal.Captured<ExpectedFailureException>().Single());
        string handlerFrame = $"{nameof(ThrowingHandler)}.{nameof(ThrowingHandler.HandleAsync)}";
        await Assert.That(thrown.StackTrace).Contains(handlerFrame);
        await Expect.EntriesAsync(host.Journal, "First.before");
    }

    [Test]
    public async Task Behavior_exception_before_the_continuation_skips_the_rest_of_the_pipeline()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<ThrowingBehavior>()
            .AddOpenBehavior(typeof(SecondBehavior<,>))
            .AddHandler<PingHandler>());

        ExpectedFailureException thrown = await Expect.ThrowsExactlyAsync<ExpectedFailureException>(
            () => host.Sender.SendAsync(new Ping("hello")));

        await Assert.That(thrown).IsSameReferenceAs(host.Journal.Captured<ExpectedFailureException>().Single());
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Handler_constructor_exception_is_not_wrapped_and_no_behavior_runs()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>))
            .AddHandler<ExplodingHandler>());

        ExpectedFailureException thrown = await Expect.ThrowsExactlyAsync<ExpectedFailureException>(
            () => host.Sender.SendAsync(new Ping("hello")));

        await Assert.That(thrown).IsSameReferenceAs(host.Journal.Captured<ExpectedFailureException>().Single());
        await Assert.That(thrown).IsNotTypeOf<TargetInvocationException>();
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Null_request_faults_the_returned_task_instead_of_throwing()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddHandler<PingHandler>()
            .AddHandler<NotifyHandler>());

        // Deliberately violates the non-null request contract.
        Task<Pong> sendingWithResponse = host.Sender.SendAsync<Pong>(null!);
        Task sendingWithoutResponse = host.Sender.SendAsync(null!);

        ArgumentNullException withResponse =
            await Expect.ThrowsExactlyAsync<ArgumentNullException>(() => sendingWithResponse);
        ArgumentNullException withoutResponse =
            await Expect.ThrowsExactlyAsync<ArgumentNullException>(() => sendingWithoutResponse);
        await Assert.That(withResponse.ParamName).IsEqualTo("request");
        await Assert.That(withoutResponse.ParamName).IsEqualTo("request");
    }

    [Test]
    public async Task Handler_invalid_operation_is_distinguishable_from_a_configuration_error()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator.AddHandler<InvalidOperationHandler>());

        InvalidOperationException fromHandler = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => host.Sender.SendAsync(new Ping("hello")));
        InvalidOperationException fromConfiguration =
            await Expect.ThrowsExactlyAsync<ArbitratorConfigurationException>(
                () => host.Sender.SendAsync(new Notify("hello")));

        await Assert.That(fromHandler).IsNotAssignableTo<ArbitratorConfigurationException>();
        await Assert.That(fromConfiguration).IsAssignableTo<ArbitratorConfigurationException>();
    }

    public sealed class ExpectedFailureException : Exception
    {
        public ExpectedFailureException()
            : base("Expected failure.")
        {
        }
    }

    public sealed class ThrowingHandler(Journal journal) : IRequestHandler<Ping, Pong>
    {
        public async Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            var failure = new ExpectedFailureException();
            journal.Capture(failure);
            throw failure;
        }
    }

    public sealed class ThrowingBehavior(Journal journal) : IPipelineBehavior<Ping, Pong>
    {
        public Task<Pong> HandleAsync(Ping request, RequestContinuation<Pong> next, CancellationToken cancellationToken)
        {
            var failure = new ExpectedFailureException();
            journal.Capture(failure);
            throw failure;
        }
    }

    public sealed class ExplodingHandler : IRequestHandler<Ping, Pong>
    {
        public ExplodingHandler(Journal journal)
        {
            var failure = new ExpectedFailureException();
            journal.Capture(failure);
            throw failure;
        }

        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new Pong(request.Message));
        }
    }

    public sealed class InvalidOperationHandler : IRequestHandler<Ping, Pong>
    {
        public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The handler rejected the request.");
        }
    }
}
