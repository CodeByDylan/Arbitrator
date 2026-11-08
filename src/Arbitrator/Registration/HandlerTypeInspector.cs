using System.Diagnostics.CodeAnalysis;

namespace Arbitrator.Registration;

/// <summary>
/// Validates handler types and discovers the handler contracts they implement.
/// </summary>
internal static class HandlerTypeInspector
{
    /// <summary>
    /// Validates a handler type and returns one contract for each request or stream handler interface it implements.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <returns>The handler contracts, one per handled request type.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// The handler type is abstract, implements no handler contract, or handles a request type that cannot be
    /// dispatched.
    /// </exception>
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public static IReadOnlyList<HandlerContract> GetContracts(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type handlerType)
    {
        if (handlerType.IsAbstract)
        {
            throw RegistrationErrors.AbstractHandler(handlerType);
        }

        HandlerContract[] contracts =
        [
            .. GenericInterfaces.FindPipelineContracts(handlerType, static kind => kind.HandlerContract)
                .Select(handlerInterface => ToContract(handlerInterface, handlerType)),
        ];

        if (contracts.Length == 0)
        {
            throw RegistrationErrors.NotAHandler(handlerType);
        }

        foreach (HandlerContract contract in contracts)
        {
            RequestTypeInspector.EnsureDispatchable(contract.RequestType, handlerType);
        }

        return contracts;
    }

    /// <summary>
    /// Creates the contract described by a closed handler interface.
    /// </summary>
    /// <param name="handlerInterface">A closed handler interface of either pipeline kind.</param>
    /// <param name="handlerType">The handler type that implements the interface.</param>
    /// <returns>The handler contract.</returns>
    private static HandlerContract ToContract(PipelineContract handlerInterface, Type handlerType)
    {
        RequestResponseTypes types = handlerInterface.Types;
        return new HandlerContract(types.RequestType, types.ResponseType, types.Kind, handlerType);
    }
}
