using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Examples;

/// <summary>
/// Queues the email that tells a customer their account was deactivated.
/// </summary>
/// <param name="journal">The journal of the test, standing in for an email queue.</param>
public sealed class DeactivationEmailSender(Journal journal) : INotificationHandler<CustomerDeactivated>
{
    /// <inheritdoc/>
    public Task HandleAsync(CustomerDeactivated notification, CancellationToken cancellationToken)
    {
        journal.Record("deactivation email queued");
        return Task.CompletedTask;
    }
}
