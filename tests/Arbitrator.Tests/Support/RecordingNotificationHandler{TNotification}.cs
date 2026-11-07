namespace Arbitrator.Tests.Support;

/// <summary>
/// A notification handler that records its name and captures the notification, the token, and itself.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
public abstract class RecordingNotificationHandler<TNotification> : INotificationHandler<TNotification>
    where TNotification : INotification
{
    private readonly Journal _journal;
    private readonly string _name;

    /// <summary>
    /// Creates a handler that records under a name.
    /// </summary>
    /// <param name="journal">The journal of the test.</param>
    /// <param name="name">The name recorded when the handler runs.</param>
    protected RecordingNotificationHandler(Journal journal, string name)
    {
        _journal = journal;
        _name = name;
    }

    /// <inheritdoc/>
    public Task HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        _journal.Record(_name);
        _journal.Capture(notification);
        _journal.Capture(new ObservedToken(_name, cancellationToken));
        _journal.Capture(this);
        return Task.CompletedTask;
    }
}
