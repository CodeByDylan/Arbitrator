using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Scanning.Fixtures;

/// <summary>
/// A scan notice handler that records "beta" and sorts after <see cref="AlphaNoticeHandler"/>.
/// </summary>
/// <param name="journal">The journal of the test.</param>
public sealed class BetaNoticeHandler(Journal journal) : RecordingNoticeHandler(journal, "beta");
