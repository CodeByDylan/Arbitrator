using System.Runtime.CompilerServices;

namespace Arbitrator.Tests.Support;

/// <summary>
/// Counts down, recording each item before yielding it and the end of the countdown, and capturing the request, the
/// token, every item, and itself.
/// </summary>
/// <param name="journal">The journal of the test.</param>
public sealed class CountdownHandler(Journal journal) : IStreamRequestHandler<Countdown, Tick>
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<Tick> HandleAsync(
        Countdown request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        journal.Capture(request);
        journal.Capture(cancellationToken);
        journal.Capture(this);

        for (int value = request.From; value > 0; value--)
        {
            var tick = new Tick(value);
            journal.Record($"handler.yield {value}");
            journal.Capture(tick);
            yield return tick;
        }

        journal.Record("handler.end");
    }
}
