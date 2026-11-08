using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Arbitrator.Tests.Examples;

/// <summary>
/// Logs which handler of a notification ran and how long it took, without logging the notification's contents.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
/// <param name="logger">The logger.</param>
public sealed class HandlerTimingBehavior<TNotification>(ILogger<HandlerTimingBehavior<TNotification>> logger)
    : INotificationBehavior<TNotification>
    where TNotification : INotification
{
    /// <inheritdoc/>
    public async Task HandleAsync(
        TNotification notification,
        Type handlerType,
        NotificationContinuation next,
        CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();

        try
        {
            await next(cancellationToken);
        }
        finally
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
            HandlerLog.Handled(logger, handlerType.Name, typeof(TNotification).Name, elapsed);
        }
    }
}
