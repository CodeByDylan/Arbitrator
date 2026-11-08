namespace Arbitrator.Tests.Support;

/// <summary>
/// A recording notification behavior named "NoticeFirst".
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
/// <param name="journal">The journal of the test.</param>
public sealed class FirstNotificationBehavior<TNotification>(Journal journal)
    : RecordingNotificationBehavior<TNotification>(journal, "NoticeFirst")
    where TNotification : INotification;
