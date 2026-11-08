namespace Arbitrator.Tests.Support;

/// <summary>
/// A notification behavior that records its name and the wrapped handler before and after the handler, and captures
/// the token it receives and itself.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
public abstract class RecordingNotificationBehavior<TNotification> : INotificationBehavior<TNotification>
    where TNotification : INotification
{
    private readonly Journal _journal;
    private readonly string _name;

    /// <summary>
    /// Creates a behavior that records under a name.
    /// </summary>
    /// <param name="journal">The journal of the test.</param>
    /// <param name="name">The name recorded around each handler.</param>
    protected RecordingNotificationBehavior(Journal journal, string name)
    {
        _journal = journal;
        _name = name;
    }

    /// <inheritdoc/>
    public async Task HandleAsync(
        TNotification notification,
        Type handlerType,
        NotificationContinuation next,
        CancellationToken cancellationToken)
    {
        _journal.Record($"{_name}.before {handlerType.Name}");
        _journal.Capture(new ObservedToken(_name, cancellationToken));
        _journal.Capture(this);

        await next(cancellationToken);

        _journal.Record($"{_name}.after {handlerType.Name}");
    }
}
