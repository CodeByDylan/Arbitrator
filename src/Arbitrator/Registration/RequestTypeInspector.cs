using System.Diagnostics.CodeAnalysis;

namespace Arbitrator.Registration;

/// <summary>
/// Verifies that a request type can be dispatched by its exact runtime type.
/// </summary>
internal static class RequestTypeInspector
{
    /// <summary>
    /// Verifies that a request type is concrete and implements exactly one request contract.
    /// </summary>
    /// <param name="requestType">The request type handled by a handler.</param>
    /// <param name="handlerType">The handler type, named when the request type is abstract.</param>
    /// <exception cref="ArbitratorConfigurationException">The request type cannot be dispatched.</exception>
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public static void EnsureDispatchable(Type requestType, Type handlerType)
    {
        if (requestType.IsAbstract)
        {
            throw RegistrationErrors.AbstractRequestType(requestType, handlerType);
        }

        Type[] requestContracts = GenericInterfaces.FindImplemented(requestType, typeof(IRequest<>));

        if (requestContracts.Length > 1)
        {
            throw RegistrationErrors.AmbiguousRequestContracts(requestType, requestContracts);
        }
    }
}
