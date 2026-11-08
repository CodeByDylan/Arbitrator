using Microsoft.Extensions.Logging;

namespace Arbitrator.Tests.Examples;

/// <summary>
/// The log messages written by <see cref="StreamLoggingBehavior{TRequest, TItem}"/>.
/// </summary>
internal static partial class StreamLog
{
    /// <summary>
    /// Logs that a stream request is about to be read.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="requestType">The request type name.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Reading {RequestType}")]
    public static partial void Reading(ILogger logger, string requestType);

    /// <summary>
    /// Logs that the read of a stream request ended, completely, early, or with a failure.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="requestType">The request type name.</param>
    /// <param name="count">How many items the read produced.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Read {Count} items of {RequestType}")]
    public static partial void Read(ILogger logger, string requestType, int count);
}
