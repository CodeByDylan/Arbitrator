using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Examples;

/// <summary>
/// Records an audit entry for every customer deactivation.
/// </summary>
/// <param name="journal">The journal of the test, standing in for an audit log.</param>
public sealed class DeactivationAuditRecorder(Journal journal) : INotificationHandler<CustomerDeactivated>
{
    /// <inheritdoc/>
    public Task HandleAsync(CustomerDeactivated notification, CancellationToken cancellationToken)
    {
        journal.Record("audit recorded");
        return Task.CompletedTask;
    }
}
