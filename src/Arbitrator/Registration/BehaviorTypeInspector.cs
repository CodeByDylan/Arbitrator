using System.Diagnostics.CodeAnalysis;
using Arbitrator.Dispatch;

namespace Arbitrator.Registration;

/// <summary>
/// Validates closed and open behavior types.
/// </summary>
internal static class BehaviorTypeInspector
{
    /// <summary>
    /// Validates a closed behavior type and discovers the behavior contracts it implements, of any pipeline kind.
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
            .. GenericInterfaces.FindPipelineContracts(behaviorType, static kind => kind.BehaviorContract)
                .Select(contract => contract.Types),
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
    /// parameters onto exactly one behavior contract in order.
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

        PipelineKind kind = FindOpenBehaviorKind(behaviorType)
            ?? throw RegistrationErrors.InvalidOpenBehaviorShape(behaviorType);

        return new OpenBehaviorRegistration(behaviorType, position, kind);
    }

    /// <summary>
    /// Finds the pipeline kind of a generic type definition that has two type parameters and implements exactly one
    /// behavior contract, of any kind, whose type arguments are those parameters in declaration order.
    /// </summary>
    /// <param name="definition">The generic type definition.</param>
    /// <returns>
    /// The kind of the behavior contract, or <see langword="null"/> when the definition doesn't have that shape.
    /// </returns>
    private static PipelineKind? FindOpenBehaviorKind(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type definition)
    {
        Type[] typeParameters = definition.GetGenericArguments();
        IReadOnlyList<PipelineContract> behaviorContracts =
            GenericInterfaces.FindPipelineContracts(definition, static kind => kind.BehaviorContract);

        bool hasOpenBehaviorShape = typeParameters.Length == 2
            && behaviorContracts is [var behaviorContract]
            && behaviorContract.Contract.GetGenericArguments().SequenceEqual(typeParameters);

        return hasOpenBehaviorShape ? behaviorContracts[0].Kind : null;
    }
}
