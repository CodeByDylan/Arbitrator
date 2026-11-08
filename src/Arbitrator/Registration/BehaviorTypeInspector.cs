using System.Diagnostics.CodeAnalysis;

namespace Arbitrator.Registration;

/// <summary>
/// Validates closed and open behavior types.
/// </summary>
internal static class BehaviorTypeInspector
{
    /// <summary>
    /// Validates a closed behavior type and discovers the request, stream, and notification behavior contracts it
    /// implements.
    /// </summary>
    /// <param name="behaviorType">The closed behavior type.</param>
    /// <param name="position">The registration position assigned to the behavior.</param>
    /// <returns>The behavior registration.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// The type is abstract or implements no behavior contract.
    /// </exception>
    public static ClosedBehaviorRegistration InspectClosed(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type behaviorType,
        int position)
    {
        if (behaviorType.IsAbstract)
        {
            throw BehaviorRegistrationErrors.AbstractBehavior(behaviorType);
        }

        RequestResponseTypes[] contracts =
        [
            .. GenericInterfaces.FindPipelineContracts(behaviorType, static kind => kind.BehaviorContract)
                .Select(contract => contract.Types),
        ];
        Type[] notificationTypes =
        [
            .. GenericInterfaces.FindImplemented(behaviorType, typeof(INotificationBehavior<>))
                .Select(contract => contract.GetGenericArguments()[0]),
        ];

        if (contracts.Length == 0 && notificationTypes.Length == 0)
        {
            throw BehaviorRegistrationErrors.NotABehavior(behaviorType);
        }

        return new ClosedBehaviorRegistration(behaviorType, position, contracts, notificationTypes);
    }

    /// <summary>
    /// Validates an open behavior type definition.
    /// </summary>
    /// <param name="behaviorType">The generic type definition.</param>
    /// <param name="position">The registration position assigned to the behavior.</param>
    /// <returns>
    /// The behavior registration: an <see cref="OpenBehaviorRegistration"/> for a request or stream behavior, or an
    /// <see cref="OpenNotificationBehaviorRegistration"/> for a notification behavior.
    /// </returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// The type is not a generic type definition, is a value type, is abstract, or does not map its type parameters
    /// onto exactly one behavior contract in order.
    /// </exception>
    public static OpenRegistration InspectOpen(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type behaviorType,
        int position)
    {
        if (!behaviorType.IsGenericTypeDefinition)
        {
            throw BehaviorRegistrationErrors.NotAnOpenBehaviorDefinition(behaviorType);
        }

        if (behaviorType.IsValueType)
        {
            throw BehaviorRegistrationErrors.ValueTypeBehavior(behaviorType);
        }

        if (behaviorType.IsAbstract)
        {
            throw BehaviorRegistrationErrors.AbstractBehavior(behaviorType);
        }

        return ClassifyOpenBehavior(behaviorType, position)
            ?? throw BehaviorRegistrationErrors.InvalidOpenBehaviorShape(behaviorType);
    }

    /// <summary>
    /// Classifies a generic type definition that implements exactly one behavior contract, of any kind, whose type
    /// arguments are the definition's type parameters in declaration order.
    /// </summary>
    /// <param name="definition">The generic type definition.</param>
    /// <param name="position">The registration position assigned to the behavior.</param>
    /// <returns>
    /// The registration for the contract's kind, or <see langword="null"/> when the definition doesn't have that
    /// shape.
    /// </returns>
    private static OpenRegistration? ClassifyOpenBehavior(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type definition,
        int position)
    {
        Type[] typeParameters = definition.GetGenericArguments();
        IReadOnlyList<PipelineContract> pipelineContracts =
            GenericInterfaces.FindPipelineContracts(definition, static kind => kind.BehaviorContract);
        Type[] notificationContracts = GenericInterfaces.FindImplemented(definition, typeof(INotificationBehavior<>));

        return (pipelineContracts, notificationContracts) switch
        {
            ([var contract], []) when MapsParametersInOrder(contract.Contract, typeParameters) =>
                new OpenBehaviorRegistration(definition, position, contract.Kind),
            ([], [var contract]) when MapsParametersInOrder(contract, typeParameters) =>
                new OpenNotificationBehaviorRegistration(definition, position),
            _ => null,
        };
    }

    /// <summary>
    /// Determines whether a contract's type arguments are exactly the given type parameters, in order.
    /// </summary>
    /// <param name="contract">The behavior contract implemented by a generic type definition.</param>
    /// <param name="typeParameters">The definition's type parameters.</param>
    /// <returns><see langword="true"/> when the definition can be closed over any contract of that shape.</returns>
    private static bool MapsParametersInOrder(Type contract, Type[] typeParameters)
    {
        return contract.GetGenericArguments().SequenceEqual(typeParameters);
    }
}
