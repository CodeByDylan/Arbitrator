namespace Arbitrator.Tests.Scanning.Fixtures;

/// <summary>
/// A request handled by a discovered handler.
/// </summary>
/// <param name="Text">The text to echo.</param>
public sealed record ScanQuery(string Text) : IRequest<string>;
