namespace Arbitrator.Tests.Examples;

/// <summary>
/// Marks a request that validates itself before it is handled.
/// </summary>
public interface IValidatable
{
    /// <summary>
    /// Validates the request.
    /// </summary>
    /// <returns>The validation errors, or an empty list when the request is valid.</returns>
    IReadOnlyList<string> Validate();
}
