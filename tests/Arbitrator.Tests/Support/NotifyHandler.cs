namespace Arbitrator.Tests.Support;

/// <summary>
/// Records a notification in the journal.
/// </summary>
/// <param name="journal">The journal of the test.</param>
public sealed class NotifyHandler(Journal journal) : IRequestHandler<Notify>
{
    /// <inheritdoc/>
    public Task HandleAsync(Notify request, CancellationToken cancellationToken)
    {
        journal.Record("handler");
        journal.Capture(request);
        return Task.CompletedTask;
    }
}
