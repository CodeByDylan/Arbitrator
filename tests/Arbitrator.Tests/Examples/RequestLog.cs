using Microsoft.Extensions.Logging;

namespace Arbitrator.Tests.Examples;

/// <summary>
/// The log messages written by <see cref="LoggingBehavior{TRequest, TResponse}"/>.
/// </summary>
internal static partial class RequestLog
{
    /// <summary>
    /// Logs that a request is about to be handled.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="requestType">The request type name.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Handling {RequestType}")]
    public static partial void Handling(ILogger logger, string requestType);

    /// <summary>
    /// Logs that a request was handled, successfully or not.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="requestType">The request type name.</param>
    /// <param name="elapsed">How long the pipeline took.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestType} in {Elapsed}")]
    public static partial void Handled(ILogger logger, string requestType, TimeSpan elapsed);
}
