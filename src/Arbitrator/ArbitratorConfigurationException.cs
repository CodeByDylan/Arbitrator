namespace Arbitrator;

/// <summary>
/// The exception thrown when Arbitrator's registrations are invalid or a request has no registered handler.
/// </summary>
public sealed class ArbitratorConfigurationException : InvalidOperationException
{
    /// <summary>
    /// Creates an exception that describes a configuration error.
    /// </summary>
    /// <param name="message">A description of the configuration error and how to correct it.</param>
    public ArbitratorConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates an exception that describes a configuration error caused by another exception.
    /// </summary>
    /// <param name="message">A description of the configuration error and how to correct it.</param>
    /// <param name="innerException">The exception that caused the configuration error.</param>
    public ArbitratorConfigurationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
