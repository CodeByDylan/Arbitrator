using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Notifications;

/// <summary>
/// Specifies what a notification behavior may do with its handler call: skip it, isolate its failure, or fail itself,
/// and the rules it must follow when it invokes its continuation.
/// </summary>
public sealed class NotificationPipelineControlTests
{
    [Test]
    public async Task Short_circuiting_behavior_skips_its_handler_and_publication_continues()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<SkipFirstHandlerBehavior>()
            .AddNotificationHandler<FirstAnnouncementHandler>()
            .AddNotificationHandler<SecondAnnouncementHandler>());

        await host.Publisher.PublishAsync(new Announcement("hello"));

        await Expect.EntriesAsync(host.Journal, "skipped FirstAnnouncementHandler", "second");
    }

    [Test]
    public async Task Behavior_that_swallows_its_handlers_failure_lets_publication_continue()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<IsolatingBehavior>()
            .AddNotificationHandler<FailingHandler>()
            .AddNotificationHandler<SecondAnnouncementHandler>());

        await host.Publisher.PublishAsync(new Announcement("hello"));

        await Expect.EntriesAsync(host.Journal, "isolated FailingHandler", "second");
    }

    [Test]
    public async Task Behavior_exception_stops_publication_and_reaches_the_caller_unchanged()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<RefusingBehavior>()
            .AddNotificationHandler<FirstAnnouncementHandler>()
            .AddNotificationHandler<SecondAnnouncementHandler>());

        InvalidTimeZoneException thrown = await Expect.ThrowsExactlyAsync<InvalidTimeZoneException>(
            () => host.Publisher.PublishAsync(new Announcement("hello")));

        await Assert.That(thrown).IsSameReferenceAs(RefusingBehavior.Refusal);
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Second_continuation_call_is_rejected_and_the_handler_runs_once()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<RepeatingBehavior>()
            .AddNotificationHandler<FirstAnnouncementHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => host.Publisher.PublishAsync(new Announcement("hello")));

        await Assert.That(exception.Message).Contains($"Behavior '{Names.Of<RepeatingBehavior>()}'");
        await Assert.That(exception.Message).Contains($"while handling '{Names.Of<Announcement>()}'");
        await Expect.EntriesAsync(host.Journal, "first");
    }

    [Test]
    public async Task Outermost_behavior_returning_a_null_task_is_reported_by_name()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<NullTaskBehavior>()
            .AddNotificationHandler<FirstAnnouncementHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => host.Publisher.PublishAsync(new Announcement("hello")));

        await Assert.That(exception.Message).Contains($"Behavior '{Names.Of<NullTaskBehavior>()}'");
        await Assert.That(exception.Message).Contains("null Task");
    }

    [Test]
    public async Task Inner_behavior_returning_a_null_task_is_reported_by_name()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>))
            .AddBehavior<NullTaskBehavior>()
            .AddNotificationHandler<FirstAnnouncementHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => host.Publisher.PublishAsync(new Announcement("hello")));

        await Assert.That(exception.Message).Contains($"Behavior '{Names.Of<NullTaskBehavior>()}'");
        await Expect.EntriesAsync(host.Journal, "NoticeFirst.before FirstAnnouncementHandler");
    }

    [Test]
    public async Task Handler_returning_a_null_task_inside_a_chain_is_reported_by_name()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>))
            .AddNotificationHandler<NullTaskHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => host.Publisher.PublishAsync(new Announcement("hello")));

        await Assert.That(exception.Message).Contains($"Handler '{Names.Of<NullTaskHandler>()}'");
        await Assert.That(exception.Message).Contains($"while handling '{Names.Of<Announcement>()}'");
        await Expect.EntriesAsync(host.Journal, "NoticeFirst.before NullTaskHandler");
    }

    public sealed class SkipFirstHandlerBehavior(Journal journal) : INotificationBehavior<Announcement>
    {
        public Task HandleAsync(
            Announcement notification,
            Type handlerType,
            NotificationContinuation next,
            CancellationToken cancellationToken)
        {
            if (handlerType != typeof(FirstAnnouncementHandler))
            {
                return next(cancellationToken);
            }

            journal.Record($"skipped {handlerType.Name}");
            return Task.CompletedTask;
        }
    }

    public sealed class IsolatingBehavior(Journal journal) : INotificationBehavior<Announcement>
    {
        public async Task HandleAsync(
            Announcement notification,
            Type handlerType,
            NotificationContinuation next,
            CancellationToken cancellationToken)
        {
            try
            {
                await next(cancellationToken);
            }
            catch (InvalidTimeZoneException)
            {
                journal.Record($"isolated {handlerType.Name}");
            }
        }
    }

    public sealed class RefusingBehavior : INotificationBehavior<Announcement>
    {
        public static InvalidTimeZoneException Refusal { get; } = new("Refused.");

        public Task HandleAsync(
            Announcement notification,
            Type handlerType,
            NotificationContinuation next,
            CancellationToken cancellationToken) => throw Refusal;
    }

    public sealed class RepeatingBehavior : INotificationBehavior<Announcement>
    {
        public async Task HandleAsync(
            Announcement notification,
            Type handlerType,
            NotificationContinuation next,
            CancellationToken cancellationToken)
        {
            await next(cancellationToken);
            await next(cancellationToken);
        }
    }

    public sealed class NullTaskBehavior : INotificationBehavior<Announcement>
    {
        // Deliberately violates the non-null task contract.
        public Task HandleAsync(
            Announcement notification,
            Type handlerType,
            NotificationContinuation next,
            CancellationToken cancellationToken) => null!;
    }

    public sealed class NullTaskHandler : INotificationHandler<Announcement>
    {
        // Deliberately violates the non-null task contract.
        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken) => null!;
    }

    public sealed class FailingHandler : INotificationHandler<Announcement>
    {
        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken) =>
            throw new InvalidTimeZoneException("The handler failed.");
    }
}
