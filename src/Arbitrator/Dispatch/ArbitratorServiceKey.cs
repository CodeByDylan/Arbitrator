namespace Arbitrator.Dispatch;

/// <summary>
/// The private service key under which handlers and behaviors are registered, keeping them separate from the
/// application's own registrations.
/// </summary>
internal sealed class ArbitratorServiceKey
{
    /// <summary>
    /// Creates the single key instance.
    /// </summary>
    private ArbitratorServiceKey()
    {
    }

    /// <summary>
    /// Gets the single key instance.
    /// </summary>
    public static ArbitratorServiceKey Instance { get; } = new();

    /// <summary>
    /// Returns a readable name for the key, which appears in dependency injection error messages.
    /// </summary>
    /// <returns>The key name.</returns>
    public override string ToString() => "Arbitrator";
}
