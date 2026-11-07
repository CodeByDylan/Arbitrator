using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Arbitrator.Tests.Examples;

/// <summary>
/// Logs the name of every request and how long its pipeline took, without logging request contents.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="logger">The logger.</param>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc/>
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestContinuation<TResponse> next,
        CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();
        RequestLog.Handling(logger, typeof(TRequest).Name);

        try
        {
            return await next(cancellationToken);
        }
        finally
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
            RequestLog.Handled(logger, typeof(TRequest).Name, elapsed);
        }
    }
}
