namespace Arbitrator.Dispatch;

/// <summary>
/// Publishes notifications to their handlers using the scope that resolved this publisher.
/// </summary>
internal sealed class Publisher : IPublisher
{
    private readonly IServiceProvider _services;
    private readonly DispatchTable _dispatchTable;

    /// <summary>
    /// Creates a publisher bound to a scope.
    /// </summary>
    /// <param name="services">The service provider of the scope that resolved this publisher.</param>
    /// <param name="dispatchTable">The dispatch table.</param>
    public Publisher(IServiceProvider services, DispatchTable dispatchTable)
    {
        _services = services;
        _dispatchTable = dispatchTable;
    }

    /// <inheritdoc/>
    public async Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (_dispatchTable.FindNotificationInvoker(notification.GetType()) is not { } invoker)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await invoker.PublishAsync(notification, _services, cancellationToken).ConfigureAwait(false);
    }
}
