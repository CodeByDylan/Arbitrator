namespace Arbitrator.Tests.Support;

/// <summary>
/// A recording stream behavior named "StreamFirst".
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TItem">The item type.</typeparam>
/// <param name="journal">The journal of the test.</param>
public sealed class FirstStreamBehavior<TRequest, TItem>(Journal journal)
    : RecordingStreamBehavior<TRequest, TItem>(journal, "StreamFirst")
    where TRequest : IStreamRequest<TItem>;
