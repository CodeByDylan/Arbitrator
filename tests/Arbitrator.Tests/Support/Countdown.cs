namespace Arbitrator.Tests.Support;

/// <summary>
/// A stream request that counts down from a number to one.
/// </summary>
/// <param name="From">The first number of the countdown.</param>
public sealed record Countdown(int From) : IStreamRequest<Tick>;
