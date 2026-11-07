namespace Arbitrator.Registration;

/// <summary>
/// One request type, the response type it produces, and the handler type registered for it.
/// </summary>
/// <param name="RequestType">The concrete request type.</param>
/// <param name="ResponseType">The response type.</param>
/// <param name="HandlerType">The handler implementation type.</param>
internal sealed record HandlerContract(Type RequestType, Type ResponseType, Type HandlerType)
{
    /// <summary>
    /// Gets the request and response types of the contract.
    /// </summary>
    public RequestResponseTypes Types => new(RequestType, ResponseType);
}
