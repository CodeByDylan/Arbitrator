using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Scanning.Fixtures;

/// <summary>
/// Holds a private nested handler, which scanning discovers although no code can name it.
/// </summary>
public static class ScanCommandHandlers
{
    /// <summary>
    /// Records "command".
    /// </summary>
    /// <param name="journal">The journal of the test.</param>
    private sealed class Handler(Journal journal) : IRequestHandler<ScanCommand>
    {
        /// <inheritdoc/>
        public Task HandleAsync(ScanCommand request, CancellationToken cancellationToken)
        {
            journal.Record("command");
            return Task.CompletedTask;
        }
    }
}
