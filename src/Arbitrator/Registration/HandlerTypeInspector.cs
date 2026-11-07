using System.Diagnostics.CodeAnalysis;

namespace Arbitrator.Registration;

/// <summary>
/// Validates handler types and discovers the handler contracts they implement.
/// </summary>
internal static class HandlerTypeInspector
{
    /// <summary>
    /// Validates a handler type and returns one contract for each handler interface it implements.
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
            .. GenericInterfaces.FindImplemented(handlerType, typeof(IRequestHandler<,>))
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
    /// <param name="handlerInterface">A closed <c>IRequestHandler&lt;TRequest, TResponse&gt;</c> interface.</param>
    /// <param name="handlerType">The handler type that implements the interface.</param>
    /// <returns>The handler contract.</returns>
    private static HandlerContract ToContract(Type handlerInterface, Type handlerType)
    {
        Type[] typeArguments = handlerInterface.GetGenericArguments();
        return new HandlerContract(typeArguments[0], typeArguments[1], handlerType);
    }
}
