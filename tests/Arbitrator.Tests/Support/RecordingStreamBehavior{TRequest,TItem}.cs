using System.Runtime.CompilerServices;

namespace Arbitrator.Tests.Support;

/// <summary>
/// A stream behavior that records its name before the read, for each item, and after the read, and captures the token
/// it receives.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TItem">The item type.</typeparam>
public abstract class RecordingStreamBehavior<TRequest, TItem> : IStreamPipelineBehavior<TRequest, TItem>
    where TRequest : IStreamRequest<TItem>
{
    private readonly Journal _journal;
    private readonly string _name;

    /// <summary>
    /// Creates a behavior that records under a name.
    /// </summary>
    /// <param name="journal">The journal of the test.</param>
    /// <param name="name">The name recorded around the read of the remainder of the pipeline.</param>
    protected RecordingStreamBehavior(Journal journal, string name)
    {
        _journal = journal;
        _name = name;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<TItem> HandleAsync(
        TRequest request,
        StreamContinuation<TItem> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _journal.Record($"{_name}.before");
        _journal.Capture(new ObservedToken(_name, cancellationToken));

        await foreach (TItem item in next(cancellationToken))
        {
            _journal.Record($"{_name}.item");
            yield return item;
        }

        _journal.Record($"{_name}.after");
    }
}
