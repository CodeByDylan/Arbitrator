namespace Arbitrator.Tests.Scanning.Fixtures;

/// <summary>
/// A request without a response, handled by a private nested handler.
/// </summary>
public sealed record ScanCommand : IRequest;
