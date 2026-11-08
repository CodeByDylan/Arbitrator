using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Scanning.Fixtures;

/// <summary>
/// A closed behavior for scan queries that records "behavior", which scanning never registers.
/// </summary>
/// <param name="journal">The journal of the test.</param>
public sealed class ScanQueryBehavior(Journal journal) : IPipelineBehavior<ScanQuery, string>
{
    /// <inheritdoc/>
    public Task<string> HandleAsync(
        ScanQuery request,
        RequestContinuation<string> next,
        CancellationToken cancellationToken)
    {
        journal.Record("behavior");
        return next(cancellationToken);
    }
}
