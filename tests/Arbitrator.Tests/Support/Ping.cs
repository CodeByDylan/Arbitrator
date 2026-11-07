namespace Arbitrator.Tests.Support;

/// <summary>
/// A request that produces a <see cref="Pong"/>.
/// </summary>
/// <param name="Message">The message to echo.</param>
public sealed record Ping(string Message) : IRequest<Pong>;
