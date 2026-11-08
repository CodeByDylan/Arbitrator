using Arbitrator.Diagnostics;

namespace Arbitrator.Registration;

/// <summary>
/// Creates the exceptions that report invalid registrations.
/// </summary>
internal static class RegistrationErrors
{
    /// <summary>
    /// Reports a second handler type for a request type that already has a handler.
    /// </summary>
    /// <param name="requestType">The request type.</param>
    /// <param name="existingHandlerType">The handler type already registered for the request type.</param>
    /// <param name="rejectedHandlerType">The handler type being registered.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException DuplicateRequestHandler(
        Type requestType,
        Type existingHandlerType,
        Type rejectedHandlerType)
    {
        return new($"Request type '{Name(requestType)}' already has handler '{Name(existingHandlerType)}'; "
            + $"cannot also register '{Name(rejectedHandlerType)}'. Each request type has exactly one handler.");
    }

    /// <summary>
    /// Reports a handler type that is registered more than once.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException DuplicateHandlerType(Type handlerType)
    {
        return new($"Handler type '{Name(handlerType)}' is already registered. "
            + "Each handler type may be registered once across all AddArbitrator calls.");
    }

    /// <summary>
    /// Reports a handler type that is abstract or an interface.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException AbstractHandler(Type handlerType)
    {
        return new($"Handler type '{Name(handlerType)}' is abstract or an interface; register a concrete class.");
    }

    /// <summary>
    /// Reports a type that implements no handler contract.
    /// </summary>
    /// <param name="handlerType">The type registered as a handler.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException NotAHandler(Type handlerType)
    {
        return new($"Type '{Name(handlerType)}' cannot be registered as a handler: it implements none of "
            + "IRequestHandler<TRequest, TResponse>, IRequestHandler<TRequest>, or "
            + "IStreamRequestHandler<TRequest, TItem>.");
    }

    /// <summary>
    /// Reports a handler for an abstract or interface request type, which exact-type dispatch can never select.
    /// </summary>
    /// <param name="requestType">The request type.</param>
    /// <param name="handlerType">The handler type.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException AbstractRequestType(Type requestType, Type handlerType)
    {
        return new($"Handler '{Name(handlerType)}' handles '{Name(requestType)}', which is abstract or an interface. "
            + "Requests are dispatched by their exact runtime type, so this handler could never run; "
            + "register a handler for each concrete request type.");
    }

    /// <summary>
    /// Reports a request type that implements more than one request contract.
    /// </summary>
    /// <param name="requestType">The request type.</param>
    /// <param name="requestContracts">The request contracts the type implements.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException AmbiguousRequestContracts(
        Type requestType,
        IReadOnlyList<Type> requestContracts)
    {
        string contracts = string.Join(", ", requestContracts.Select(Name));

        return new($"Request type '{Name(requestType)}' implements more than one request contract ({contracts}). "
            + "A request type must implement exactly one of IRequest, IRequest<TResponse>, or IStreamRequest<TItem>.");
    }

    /// <summary>
    /// Reports a registration attempted after the dispatch table was built.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException RegistrationFrozen()
    {
        return new("Arbitrator registrations cannot change once a service provider has attempted to resolve "
            + "ISender or IPublisher. Complete all AddArbitrator calls before building the service provider.");
    }

    /// <summary>
    /// Reports a registration attempted on a read-only service collection.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException ServiceCollectionIsReadOnly()
    {
        return new("The service collection is read-only, typically because the host has been built. "
            + "Complete all AddArbitrator calls before building the host.");
    }

    /// <summary>
    /// Formats a type for a message.
    /// </summary>
    /// <param name="type">The type to format.</param>
    /// <returns>The formatted type name.</returns>
    private static string Name(Type type) => TypeNameFormatter.Format(type);
}
