namespace Arbitrator.Tests.Support;

/// <summary>
/// A notification handled by the shared announcement handlers.
/// </summary>
/// <param name="Message">The announced message.</param>
public sealed record Announcement(string Message) : INotification;
