namespace Arbitrator.Tests.Scanning.Fixtures;

/// <summary>
/// A struct implementing a notification handler contract, which scanning skips.
/// </summary>
public readonly struct NoticeStructHandler : INotificationHandler<ScanNotice>
{
    /// <inheritdoc/>
    public Task HandleAsync(ScanNotice notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
