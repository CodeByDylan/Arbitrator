namespace Arbitrator.Tests.Support;

/// <summary>
/// A behavior that records its name before and after the remainder of the pipeline, and captures the token it
/// receives.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public abstract class RecordingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly Journal _journal;
    private readonly string _name;

    /// <summary>
    /// Creates a behavior that records under a name.
    /// </summary>
    /// <param name="journal">The journal of the test.</param>
    /// <param name="name">The name recorded before and after the remainder of the pipeline.</param>
    protected RecordingBehavior(Journal journal, string name)
    {
        _journal = journal;
        _name = name;
    }

    /// <inheritdoc/>
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestContinuation<TResponse> next,
        CancellationToken cancellationToken)
    {
        _journal.Record($"{_name}.before");
        _journal.Capture(new ObservedToken(_name, cancellationToken));

        TResponse response = await next(cancellationToken);

        _journal.Record($"{_name}.after");
        return response;
    }
}
