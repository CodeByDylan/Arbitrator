namespace Arbitrator;

/// <summary>
/// Represents the absence of a response. It is the response type of requests that produce no response.
/// </summary>
public readonly record struct Unit
{
    /// <summary>
    /// Gets the only value of <see cref="Unit"/>.
    /// </summary>
    public static Unit Value => default;
}
