namespace Arbitrator.Tests.Examples;

/// <summary>
/// Rejects requests that report validation errors before they reach the rest of the pipeline.
/// </summary>
/// <typeparam name="TRequest">A request type that validates itself.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IValidatable
{
    /// <inheritdoc/>
    public Task<TResponse> HandleAsync(
        TRequest request,
        RequestContinuation<TResponse> next,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = request.Validate();

        if (errors.Count > 0)
        {
            throw new RequestValidationException(typeof(TRequest).Name, errors);
        }

        return next(cancellationToken);
    }
}
