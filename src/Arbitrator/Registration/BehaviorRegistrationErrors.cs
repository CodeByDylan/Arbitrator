using Arbitrator.Diagnostics;

namespace Arbitrator.Registration;

/// <summary>
/// Creates the exceptions that report invalid behavior registrations.
/// </summary>
internal static class BehaviorRegistrationErrors
{
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
        return new($"Open behavior '{Name(behaviorType)}' must be a generic type definition that implements exactly "
            + "one behavior contract using its type parameters in order: IPipelineBehavior<TRequest, TResponse> or "
            + "IStreamPipelineBehavior<TRequest, TItem> with two type parameters, or "
            + "INotificationBehavior<TNotification> with one.");
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
            + "it implements none of IPipelineBehavior<TRequest, TResponse>, "
            + "IStreamPipelineBehavior<TRequest, TItem>, or INotificationBehavior<TNotification>.");
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
    /// Reports two registrations that would place the same closed behavior type twice in one request pipeline.
    /// </summary>
    /// <param name="conflict">The conflicting registrations.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException BehaviorWouldRunTwice(BehaviorConflict conflict)
    {
        return WouldRunTwice(conflict, "request");
    }

    /// <summary>
    /// Reports two registrations that would place the same closed behavior type twice around a notification's
    /// handlers.
    /// </summary>
    /// <param name="conflict">The conflicting registrations.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException NotificationBehaviorWouldRunTwice(BehaviorConflict conflict)
    {
        return WouldRunTwice(conflict, "notification");
    }

    /// <summary>
    /// Reports a closed behavior none of whose request or notification types has a registered handler.
    /// </summary>
    /// <param name="registration">The closed behavior registration.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException ClosedBehaviorWithoutHandler(
        ClosedBehaviorRegistration registration)
    {
        string contracts = string.Join(", ", [
            .. registration.Contracts.Select(BehaviorContract),
            .. registration.NotificationTypes.Select(ContractNames.NotificationBehavior),
        ]);

        return new($"Behavior '{Name(registration.BehaviorType)}' implements {contracts}, "
            + "but none of the types it applies to has a registered handler.");
    }

    /// <summary>
    /// Reports two registrations that would place the same closed behavior type twice around one handler.
    /// </summary>
    /// <param name="conflict">The conflicting registrations.</param>
    /// <param name="targetDescription">What the target type is, such as "request".</param>
    /// <returns>The exception to throw.</returns>
    private static ArbitratorConfigurationException WouldRunTwice(BehaviorConflict conflict, string targetDescription)
    {
        return new($"Behavior '{Name(conflict.ClosedBehaviorType)}' would run twice for {targetDescription} "
            + $"'{Name(conflict.TargetType)}': it is produced by '{Name(conflict.Existing.BehaviorType)}' "
            + $"at position {conflict.Existing.Position} and by '{Name(conflict.Rejected.BehaviorType)}' "
            + $"at position {conflict.Rejected.Position}.");
    }

    /// <summary>
    /// Formats a type for a message.
    /// </summary>
    /// <param name="type">The type to format.</param>
    /// <returns>The formatted type name.</returns>
    private static string Name(Type type) => TypeNameFormatter.Format(type);

    /// <summary>
    /// Formats the behavior contract of a closed behavior registration.
    /// </summary>
    /// <param name="types">The type arguments and kind of the contract.</param>
    /// <returns>The formatted contract.</returns>
    private static string BehaviorContract(RequestResponseTypes types)
    {
        return ContractNames.Behavior(types.Kind.BehaviorContract, types.RequestType, types.ResponseType);
    }
}
