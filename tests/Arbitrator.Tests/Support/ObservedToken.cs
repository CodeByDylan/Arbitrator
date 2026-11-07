namespace Arbitrator.Tests.Support;

/// <summary>
/// A cancellation token observed by a named pipeline component.
/// </summary>
/// <param name="Observer">The name of the component.</param>
/// <param name="Token">The token the component received.</param>
public sealed record ObservedToken(string Observer, CancellationToken Token);
