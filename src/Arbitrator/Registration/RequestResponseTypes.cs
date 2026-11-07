namespace Arbitrator.Registration;

/// <summary>
/// The request and response type arguments of a handler or behavior contract.
/// </summary>
/// <param name="RequestType">The request type.</param>
/// <param name="ResponseType">The response type.</param>
internal readonly record struct RequestResponseTypes(Type RequestType, Type ResponseType);
