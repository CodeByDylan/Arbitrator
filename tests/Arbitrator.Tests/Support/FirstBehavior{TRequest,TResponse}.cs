namespace Arbitrator.Tests.Support;

/// <summary>
/// A recording behavior named "First".
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="journal">The journal of the test.</param>
public sealed class FirstBehavior<TRequest, TResponse>(Journal journal)
    : RecordingBehavior<TRequest, TResponse>(journal, "First")
    where TRequest : IRequest<TResponse>;
