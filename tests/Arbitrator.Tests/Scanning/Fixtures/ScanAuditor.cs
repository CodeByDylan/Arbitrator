using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Scanning.Fixtures;

/// <summary>
/// Handles both a request and a notification, recording "auditor request" and "auditor notice".
/// </summary>
/// <param name="journal">The journal of the test.</param>
public sealed class ScanAuditor(Journal journal) : IRequestHandler<ScanAudit>, INotificationHandler<ScanNotice>
{
    /// <inheritdoc/>
    public Task HandleAsync(ScanAudit request, CancellationToken cancellationToken)
    {
        journal.Record("auditor request");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task HandleAsync(ScanNotice notification, CancellationToken cancellationToken)
    {
        journal.Record("auditor notice");
        return Task.CompletedTask;
    }
}
