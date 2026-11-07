namespace Arbitrator.Tests.Examples;

/// <summary>
/// The exception thrown when a request is invalid.
/// </summary>
public sealed class RequestValidationException : Exception
{
    /// <summary>
    /// Creates an exception that lists the validation errors of a request.
    /// </summary>
    /// <param name="requestName">The request type name.</param>
    /// <param name="errors">The validation errors.</param>
    public RequestValidationException(string requestName, IReadOnlyList<string> errors)
        : base($"Request '{requestName}' is invalid: {string.Join(" ", errors)}")
    {
        Errors = errors;
    }

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }
}
