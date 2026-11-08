using Microsoft.Extensions.Logging;

namespace Arbitrator.Tests.Examples;

/// <summary>
/// The log messages written by <see cref="HandlerTimingBehavior{TNotification}"/>.
/// </summary>
internal static partial class HandlerLog
{
    /// <summary>
    /// Logs that a notification handler finished, successfully or not.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="handlerType">The handler type name.</param>
    /// <param name="notificationType">The notification type name.</param>
    /// <param name="elapsed">How long the handler took.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "{HandlerType} handled {NotificationType} in {Elapsed}")]
    public static partial void Handled(ILogger logger, string handlerType, string notificationType, TimeSpan elapsed);
}
