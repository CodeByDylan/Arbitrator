namespace Arbitrator.Tests.Support;

/// <summary>
/// Echoes a ping and records the request, the response, the token, and itself in the journal.
/// </summary>
/// <param name="journal">The journal of the test.</param>
public sealed class PingHandler(Journal journal) : IRequestHandler<Ping, Pong>
{
    /// <inheritdoc/>
    public Task<Pong> HandleAsync(Ping request, CancellationToken cancellationToken)
    {
        var response = new Pong(request.Message);

        journal.Record("handler");
        journal.Capture(request);
        journal.Capture(response);
        journal.Capture(cancellationToken);
        journal.Capture(this);

        return Task.FromResult(response);
    }
}
