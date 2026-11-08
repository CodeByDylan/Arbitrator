namespace Arbitrator.Tests.Support;

/// <summary>
/// A synchronization context that runs posted work on the thread pool with itself as the current context, so tests
/// can tell whether code runs on it.
/// </summary>
public sealed class MarkedSynchronizationContext : SynchronizationContext
{
    /// <summary>
    /// Gets a value indicating whether the calling code runs on a marked context.
    /// </summary>
    public static bool IsCurrent => Current is MarkedSynchronizationContext;

    /// <inheritdoc/>
    public override void Post(SendOrPostCallback d, object? state)
    {
        ThreadPool.QueueUserWorkItem(_ => RunOnThisContext(d, state));
    }

    /// <summary>
    /// Runs a callback with this context as the current context.
    /// </summary>
    /// <param name="callback">The posted callback.</param>
    /// <param name="state">The state passed to the callback.</param>
    private void RunOnThisContext(SendOrPostCallback callback, object? state)
    {
        SynchronizationContext? previous = Current;
        SetSynchronizationContext(this);

        try
        {
            callback(state);
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }
}
