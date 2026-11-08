namespace Arbitrator.Tests.Support;

/// <summary>
/// A recording stream behavior named "StreamSecond".
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TItem">The item type.</typeparam>
/// <param name="journal">The journal of the test.</param>
public sealed class SecondStreamBehavior<TRequest, TItem>(Journal journal)
    : RecordingStreamBehavior<TRequest, TItem>(journal, "StreamSecond")
    where TRequest : IStreamRequest<TItem>;
