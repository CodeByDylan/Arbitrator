namespace Arbitrator.Tests.Support;

/// <summary>
/// A request that produces no response.
/// </summary>
/// <param name="Message">The message to record.</param>
public sealed record Notify(string Message) : IRequest;
