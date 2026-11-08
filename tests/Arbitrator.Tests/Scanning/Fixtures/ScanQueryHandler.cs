using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Scanning.Fixtures;

/// <summary>
/// An internal handler that echoes a scan query and records "query".
/// </summary>
/// <param name="journal">The journal of the test.</param>
internal sealed class ScanQueryHandler(Journal journal) : IRequestHandler<ScanQuery, string>
{
    /// <inheritdoc/>
    public Task<string> HandleAsync(ScanQuery request, CancellationToken cancellationToken)
    {
        journal.Record("query");
        return Task.FromResult(request.Text);
    }
}
