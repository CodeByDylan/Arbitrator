namespace Arbitrator.Tests.Support;

/// <summary>
/// An announcement handler that records "second".
/// </summary>
/// <param name="journal">The journal of the test.</param>
public sealed class SecondAnnouncementHandler(Journal journal)
    : RecordingNotificationHandler<Announcement>(journal, "second");
