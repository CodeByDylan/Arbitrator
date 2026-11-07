using System.Diagnostics.CodeAnalysis;

namespace Arbitrator.Registration;

/// <summary>
/// Validates closed and open behavior types.
/// </summary>
internal static class BehaviorTypeInspector
{
    /// <summary>
    /// Validates a closed behavior type and discovers the behavior contracts it implements.
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
            throw RegistrationErrors.AbstractBehavior(behaviorType);
        }

        RequestResponseTypes[] contracts =
        [
            .. GenericInterfaces.FindImplemented(behaviorType, typeof(IPipelineBehavior<,>))
                .Select(ToRequestResponseTypes),
        ];

        if (contracts.Length == 0)
        {
            throw RegistrationErrors.NotABehavior(behaviorType);
        }

        return new ClosedBehaviorRegistration(behaviorType, position, contracts);
    }

    /// <summary>
    /// Validates an open behavior type definition.
    /// </summary>
    /// <param name="behaviorType">The generic type definition.</param>
    /// <param name="position">The registration position assigned to the behavior.</param>
    /// <returns>The behavior registration.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// The type is not a generic type definition, is a value type, is abstract, or does not map its two type
    /// parameters onto the behavior contract in order.
    /// </exception>
    public static OpenBehaviorRegistration InspectOpen(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type behaviorType,
        int position)
    {
        if (!behaviorType.IsGenericTypeDefinition)
        {
            throw RegistrationErrors.NotAnOpenBehaviorDefinition(behaviorType);
        }

        if (behaviorType.IsValueType)
        {
            throw RegistrationErrors.ValueTypeBehavior(behaviorType);
        }

        if (behaviorType.IsAbstract)
        {
            throw RegistrationErrors.AbstractBehavior(behaviorType);
        }

        if (!HasOpenBehaviorShape(behaviorType))
        {
            throw RegistrationErrors.InvalidOpenBehaviorShape(behaviorType);
        }

        return new OpenBehaviorRegistration(behaviorType, position);
    }

    /// <summary>
    /// Determines whether a generic type definition has two type parameters and implements exactly one behavior
    /// contract whose type arguments are those parameters in declaration order.
    /// </summary>
    /// <param name="definition">The generic type definition.</param>
    /// <returns><see langword="true"/> when the definition can be closed over any request and response type.</returns>
    private static bool HasOpenBehaviorShape(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type definition)
    {
        Type[] typeParameters = definition.GetGenericArguments();
        Type[] behaviorContracts = GenericInterfaces.FindImplemented(definition, typeof(IPipelineBehavior<,>));

        return typeParameters.Length == 2
            && behaviorContracts is [var behaviorContract]
            && behaviorContract.GetGenericArguments().SequenceEqual(typeParameters);
    }

    /// <summary>
    /// Reads the request and response types of a closed behavior interface.
    /// </summary>
    /// <param name="behaviorInterface">A closed <c>IPipelineBehavior&lt;TRequest, TResponse&gt;</c> interface.</param>
    /// <returns>The request and response types.</returns>
    private static RequestResponseTypes ToRequestResponseTypes(Type behaviorInterface)
    {
        Type[] typeArguments = behaviorInterface.GetGenericArguments();
        return new RequestResponseTypes(typeArguments[0], typeArguments[1]);
    }
}
