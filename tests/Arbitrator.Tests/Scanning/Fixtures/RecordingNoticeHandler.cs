using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Scanning.Fixtures;

/// <summary>
/// An abstract scan notice handler that records its name, which scanning skips.
/// </summary>
public abstract class RecordingNoticeHandler : INotificationHandler<ScanNotice>
{
    private readonly Journal _journal;
    private readonly string _name;

    /// <summary>
    /// Creates a handler that records under a name.
    /// </summary>
    /// <param name="journal">The journal of the test.</param>
    /// <param name="name">The name recorded when the handler runs.</param>
    protected RecordingNoticeHandler(Journal journal, string name)
    {
        _journal = journal;
        _name = name;
    }

    /// <inheritdoc/>
    public Task HandleAsync(ScanNotice notification, CancellationToken cancellationToken)
    {
        _journal.Record(_name);
        return Task.CompletedTask;
    }
}
