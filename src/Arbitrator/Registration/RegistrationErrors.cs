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
        return new($"Type '{Name(handlerType)}' cannot be registered as a handler: it implements neither "
            + "IRequestHandler<TRequest, TResponse> nor IRequestHandler<TRequest>.");
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
            + "A request type must implement exactly one of IRequest or IRequest<TResponse>.");
    }

    /// <summary>
    /// Reports a type passed as an open behavior that is not a generic type definition.
    /// </summary>
    /// <param name="behaviorType">The type passed as an open behavior.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException NotAnOpenBehaviorDefinition(Type behaviorType)
    {
        string name = Name(behaviorType);
        string definition = behaviorType.IsConstructedGenericType
            ? Name(behaviorType.GetGenericTypeDefinition())
            : string.Empty;

        return new(behaviorType switch
        {
            { IsGenericParameter: true } =>
                $"'{name}' is a generic type parameter, not a behavior type. Pass a generic type definition, "
                + "such as typeof(LoggingBehavior<,>).",
            { IsConstructedGenericType: true, ContainsGenericParameters: true } =>
                $"'{name}' is partially closed. Pass the generic type definition '{definition}'.",
            { IsConstructedGenericType: true } =>
                $"'{name}' is a closed type. Register it with AddBehavior<TBehavior>() or pass the generic type "
                + $"definition '{definition}'.",
            _ => $"'{name}' is not a generic type definition. Register it with AddBehavior<TBehavior>().",
        });
    }

    /// <summary>
    /// Reports a behavior type that is a value type.
    /// </summary>
    /// <param name="behaviorType">The behavior type.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException ValueTypeBehavior(Type behaviorType)
    {
        return new($"Behavior type '{Name(behaviorType)}' is a value type; behaviors must be classes.");
    }

    /// <summary>
    /// Reports an open behavior whose generic shape does not match the behavior contract.
    /// </summary>
    /// <param name="behaviorType">The open behavior type.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException InvalidOpenBehaviorShape(Type behaviorType)
    {
        return new($"Open behavior '{Name(behaviorType)}' must be a generic type definition with exactly two type "
            + "parameters that implements IPipelineBehavior<TRequest, TResponse> using those parameters in order.");
    }

    /// <summary>
    /// Reports a behavior type that is abstract or an interface.
    /// </summary>
    /// <param name="behaviorType">The behavior type.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException AbstractBehavior(Type behaviorType)
    {
        return new($"Behavior type '{Name(behaviorType)}' is abstract or an interface; register a concrete class.");
    }

    /// <summary>
    /// Reports a type that implements no behavior contract.
    /// </summary>
    /// <param name="behaviorType">The type registered as a behavior.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException NotABehavior(Type behaviorType)
    {
        return new($"Type '{Name(behaviorType)}' cannot be registered as a behavior: "
            + "it does not implement IPipelineBehavior<TRequest, TResponse>.");
    }

    /// <summary>
    /// Reports a behavior type that is registered more than once.
    /// </summary>
    /// <param name="existing">The existing registration of the behavior type.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException DuplicateBehavior(BehaviorRegistration existing)
    {
        return new($"Behavior '{Name(existing.BehaviorType)}' is already registered at position {existing.Position}. "
            + "Each behavior may be registered once; its registration position defines its place in the pipeline.");
    }

    /// <summary>
    /// Reports two registrations that would place the same closed behavior type twice in one pipeline.
    /// </summary>
    /// <param name="conflict">The conflicting registrations.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException BehaviorWouldRunTwice(BehaviorConflict conflict)
    {
        return new($"Behavior '{Name(conflict.ClosedBehaviorType)}' would run twice for request "
            + $"'{Name(conflict.RequestType)}': it is produced by '{Name(conflict.Existing.BehaviorType)}' "
            + $"at position {conflict.Existing.Position} and by '{Name(conflict.Rejected.BehaviorType)}' "
            + $"at position {conflict.Rejected.Position}.");
    }

    /// <summary>
    /// Reports a closed behavior whose request types have no registered handler.
    /// </summary>
    /// <param name="registration">The closed behavior registration.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException ClosedBehaviorWithoutHandler(
        ClosedBehaviorRegistration registration)
    {
        string contracts = string.Join(", ", registration.Contracts.Select(
            contract => ContractNames.Behavior(contract.RequestType, contract.ResponseType)));

        return new($"Behavior '{Name(registration.BehaviorType)}' implements {contracts}, "
            + "but no handler is registered for its request type.");
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
