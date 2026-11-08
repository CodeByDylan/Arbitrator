namespace Arbitrator.Tests.Support;

/// <summary>
/// An item of a <see cref="Countdown"/> stream.
/// </summary>
/// <param name="Value">The number counted down to.</param>
public sealed record Tick(int Value);
