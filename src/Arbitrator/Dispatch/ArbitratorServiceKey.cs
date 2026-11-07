namespace Arbitrator.Dispatch;

/// <summary>
/// A private service key under which Arbitrator registers its components, keeping them separate from the
/// application's own registrations.
/// </summary>
/// <remarks>
/// Request components and notification handlers use separate keys, so a class registered in both roles has a
/// distinct registration for each role.
/// </remarks>
internal sealed class ArbitratorServiceKey
{
    private readonly string _name;

    /// <summary>
    /// Creates a named key.
    /// </summary>
    /// <param name="name">The name that appears in dependency injection error messages.</param>
    private ArbitratorServiceKey(string name)
    {
        _name = name;
    }

    /// <summary>
    /// Gets the key under which request handlers and pipeline behaviors are registered.
    /// </summary>
    public static ArbitratorServiceKey RequestComponents { get; } = new("Arbitrator");

    /// <summary>
    /// Gets the key under which notification handlers are registered.
    /// </summary>
    public static ArbitratorServiceKey NotificationHandlers { get; } = new("Arbitrator notification handlers");

    /// <summary>
    /// Returns a readable name for the key, which appears in dependency injection error messages.
    /// </summary>
    /// <returns>The key name.</returns>
    public override string ToString() => _name;
}
