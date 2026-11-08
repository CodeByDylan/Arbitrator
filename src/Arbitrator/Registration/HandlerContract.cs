using Arbitrator.Dispatch;

namespace Arbitrator.Registration;

/// <summary>
/// One request type, the response or item type it produces, and the handler type registered for it.
/// </summary>
/// <param name="RequestType">The concrete request type.</param>
/// <param name="ResponseType">The response type, or the item type of a stream request.</param>
/// <param name="Kind">The kind of pipeline the request is dispatched through.</param>
/// <param name="HandlerType">The handler implementation type.</param>
internal sealed record HandlerContract(Type RequestType, Type ResponseType, PipelineKind Kind, Type HandlerType)
{
    /// <summary>
    /// Gets the type arguments and kind of the contract.
    /// </summary>
    public RequestResponseTypes Types => new(RequestType, ResponseType, Kind);
}
