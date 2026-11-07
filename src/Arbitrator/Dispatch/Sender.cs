namespace Arbitrator.Dispatch;

/// <summary>
/// Sends requests through their pipelines using the scope that resolved this sender.
/// </summary>
internal sealed class Sender : ISender
{
    private readonly IServiceProvider _services;
    private readonly DispatchTable _dispatchTable;

    /// <summary>
    /// Creates a sender bound to a scope.
    /// </summary>
    /// <param name="services">The service provider of the scope that resolved this sender.</param>
    /// <param name="dispatchTable">The dispatch table.</param>
    public Sender(IServiceProvider services, DispatchTable dispatchTable)
    {
        _services = services;
        _dispatchTable = dispatchTable;
    }

    /// <inheritdoc/>
    public async Task<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequestInvoker<TResponse> invoker = _dispatchTable.GetInvoker<TResponse>(request.GetType());
        cancellationToken.ThrowIfCancellationRequested();

        return await invoker.InvokeAsync(request, _services, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SendAsync(IRequest request, CancellationToken cancellationToken = default)
    {
        await SendAsync<Unit>(request, cancellationToken).ConfigureAwait(false);
    }
}
