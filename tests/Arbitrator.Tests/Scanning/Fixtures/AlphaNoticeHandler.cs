using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Scanning.Fixtures;

/// <summary>
/// A scan notice handler that records "alpha" and sorts first by name.
/// </summary>
/// <param name="journal">The journal of the test.</param>
public sealed class AlphaNoticeHandler(Journal journal) : RecordingNoticeHandler(journal, "alpha");
