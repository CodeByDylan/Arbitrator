namespace Arbitrator.Tests.Support;

/// <summary>
/// An announcement handler that records "first".
/// </summary>
/// <param name="journal">The journal of the test.</param>
public sealed class FirstAnnouncementHandler(Journal journal)
    : RecordingNotificationHandler<Announcement>(journal, "first");
