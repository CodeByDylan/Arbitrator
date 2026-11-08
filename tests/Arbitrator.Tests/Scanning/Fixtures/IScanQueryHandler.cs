namespace Arbitrator.Tests.Scanning.Fixtures;

/// <summary>
/// An interface that extends a handler contract, which scanning skips.
/// </summary>
public interface IScanQueryHandler : IRequestHandler<ScanQuery, string>;
