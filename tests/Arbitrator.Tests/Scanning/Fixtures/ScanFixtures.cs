namespace Arbitrator.Tests.Scanning.Fixtures;

/// <summary>
/// Selects the scanning fixtures out of the test assembly.
/// </summary>
public static class ScanFixtures
{
    /// <summary>
    /// Determines whether a type is a scanning fixture.
    /// </summary>
    /// <param name="type">A type found by a scan.</param>
    /// <returns><see langword="true"/> when the type belongs to the fixtures namespace.</returns>
    public static bool Contains(Type type) => type.Namespace == typeof(ScanFixtures).Namespace;
}
