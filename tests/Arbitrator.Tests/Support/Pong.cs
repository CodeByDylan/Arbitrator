namespace Arbitrator.Tests.Support;

/// <summary>
/// The response to a <see cref="Ping"/>.
/// </summary>
/// <param name="Message">The echoed message.</param>
public sealed record Pong(string Message);
