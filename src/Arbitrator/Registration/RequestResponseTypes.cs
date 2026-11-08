using Arbitrator.Dispatch;

namespace Arbitrator.Registration;

/// <summary>
/// The type arguments of a handler or behavior contract, and the kind of pipeline the contract belongs to.
/// </summary>
/// <param name="RequestType">The request type.</param>
/// <param name="ResponseType">The response type, or the item type of a stream request.</param>
/// <param name="Kind">The kind of pipeline.</param>
internal readonly record struct RequestResponseTypes(Type RequestType, Type ResponseType, PipelineKind Kind);
