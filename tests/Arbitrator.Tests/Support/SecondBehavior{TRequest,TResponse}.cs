namespace Arbitrator.Tests.Support;

/// <summary>
/// A recording behavior named "Second".
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="journal">The journal of the test.</param>
public sealed class SecondBehavior<TRequest, TResponse>(Journal journal)
    : RecordingBehavior<TRequest, TResponse>(journal, "Second")
    where TRequest : IRequest<TResponse>;
