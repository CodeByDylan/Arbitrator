using System.Reflection;
using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Notifications;

/// <summary>
/// Specifies that the first failing handler stops publication and its exception reaches the caller unwrapped.
/// </summary>
public sealed class NotificationExceptionTests
{
    [Test]
    public async Task Handler_exception_stops_publication_and_keeps_the_effects_of_earlier_handlers()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<FirstAnnouncementHandler>()
            .AddNotificationHandler<ThrowingHandler>()
            .AddNotificationHandler<SecondAnnouncementHandler>());

        ExpectedFailureException thrown = await Expect.ThrowsExactlyAsync<ExpectedFailureException>(
            () => host.Publisher.PublishAsync(new Announcement("hello")));

        await Assert.That(thrown).IsSameReferenceAs(host.Journal.Captured<ExpectedFailureException>().Single());
        string handlerFrame = $"{nameof(ThrowingHandler)}.{nameof(ThrowingHandler.HandleAsync)}";
        await Assert.That(thrown.StackTrace).Contains(handlerFrame);
        await Expect.EntriesAsync(host.Journal, "first");
    }

    [Test]
    public async Task Handler_returning_a_null_task_is_reported_by_name()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<NullTaskHandler>());

        InvalidOperationException exception = await Expect.ThrowsExactlyAsync<InvalidOperationException>(
            () => host.Publisher.PublishAsync(new Announcement("hello")));

        await Assert.That(exception.Message).Contains($"Handler '{Names.Of<NullTaskHandler>()}'");
        await Assert.That(exception.Message).Contains($"while handling '{Names.Of<Announcement>()}'");
    }

    [Test]
    public async Task Handler_constructor_exception_is_not_wrapped_and_no_handler_runs()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<FirstAnnouncementHandler>()
            .AddNotificationHandler<ExplodingHandler>());

        ExpectedFailureException thrown = await Expect.ThrowsExactlyAsync<ExpectedFailureException>(
            () => host.Publisher.PublishAsync(new Announcement("hello")));

        await Assert.That(thrown).IsNotTypeOf<TargetInvocationException>();
        await Assert.That(host.Journal.Entries).IsEmpty();
    }

    [Test]
    public async Task Null_notification_faults_the_returned_task_instead_of_throwing()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<FirstAnnouncementHandler>());

        // Deliberately violates the non-null notification contract.
        Task publishing = host.Publisher.PublishAsync(null!);

        ArgumentNullException exception = await Expect.ThrowsExactlyAsync<ArgumentNullException>(() => publishing);
        await Assert.That(exception.ParamName).IsEqualTo("notification");
    }

    public sealed class ExpectedFailureException : Exception
    {
        public ExpectedFailureException()
            : base("Expected failure.")
        {
        }
    }

    public sealed class ThrowingHandler(Journal journal) : INotificationHandler<Announcement>
    {
        public async Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            await Task.Yield();
            var failure = new ExpectedFailureException();
            journal.Capture(failure);
            throw failure;
        }
    }

    public sealed class NullTaskHandler : INotificationHandler<Announcement>
    {
        // Deliberately breaks the non-null Task contract.
        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken) => null!;
    }

    public sealed class ExplodingHandler : INotificationHandler<Announcement>
    {
        public ExplodingHandler()
        {
            throw new ExpectedFailureException();
        }

        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
