using System.Diagnostics.CodeAnalysis;

namespace Arbitrator.Registration;

/// <summary>
/// Validates notification handler types and discovers the notification contracts they implement.
/// </summary>
internal static class NotificationHandlerTypeInspector
{
    /// <summary>
    /// Validates a notification handler type and returns one contract for each notification type it handles.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <returns>The notification handler contracts.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// The handler type is abstract, implements no notification handler contract, or handles an abstract
    /// notification type.
    /// </exception>
    public static IReadOnlyList<NotificationHandlerContract> GetContracts(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type handlerType)
    {
        if (handlerType.IsAbstract)
        {
            throw RegistrationErrors.AbstractHandler(handlerType);
        }

        NotificationHandlerContract[] contracts =
        [
            .. GenericInterfaces.FindImplemented(handlerType, typeof(INotificationHandler<>))
                .Select(handlerInterface => new NotificationHandlerContract(
                    handlerInterface.GetGenericArguments()[0],
                    handlerType)),
        ];

        if (contracts.Length == 0)
        {
            throw NotificationRegistrationErrors.NotANotificationHandler(handlerType);
        }

        foreach (NotificationHandlerContract contract in contracts)
        {
            EnsurePublishable(contract);
        }

        return contracts;
    }

    /// <summary>
    /// Rejects a contract whose notification type is abstract or an interface.
    /// </summary>
    /// <param name="contract">The notification handler contract.</param>
    /// <exception cref="ArbitratorConfigurationException">The notification type cannot be published.</exception>
    private static void EnsurePublishable(NotificationHandlerContract contract)
    {
        if (contract.NotificationType.IsAbstract)
        {
            throw NotificationRegistrationErrors.AbstractNotificationType(
                contract.NotificationType,
                contract.HandlerType);
        }
    }
}
