namespace Arbitrator.Tests.Support;

/// <summary>
/// A recording notification behavior named "NoticeSecond".
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
/// <param name="journal">The journal of the test.</param>
public sealed class SecondNotificationBehavior<TNotification>(Journal journal)
    : RecordingNotificationBehavior<TNotification>(journal, "NoticeSecond")
    where TNotification : INotification;
