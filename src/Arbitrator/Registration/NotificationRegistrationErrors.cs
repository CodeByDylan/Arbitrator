using Arbitrator.Diagnostics;

namespace Arbitrator.Registration;

/// <summary>
/// Creates the exceptions that report invalid notification handler registrations.
/// </summary>
internal static class NotificationRegistrationErrors
{
    /// <summary>
    /// Reports a notification handler type that is registered more than once.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException DuplicateHandlerType(Type handlerType)
    {
        return new($"Notification handler type '{Name(handlerType)}' is already registered. "
            + "Each notification handler type may be registered once across all AddArbitrator calls.");
    }

    /// <summary>
    /// Reports a type that implements no notification handler contract.
    /// </summary>
    /// <param name="handlerType">The type registered as a notification handler.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException NotANotificationHandler(Type handlerType)
    {
        return new($"Type '{Name(handlerType)}' cannot be registered as a notification handler: "
            + "it does not implement INotificationHandler<TNotification>.");
    }

    /// <summary>
    /// Reports a handler for an abstract or interface notification type, which exact-type publishing can never
    /// select.
    /// </summary>
    /// <param name="notificationType">The notification type.</param>
    /// <param name="handlerType">The handler type.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException AbstractNotificationType(Type notificationType, Type handlerType)
    {
        return new($"Notification handler '{Name(handlerType)}' handles '{Name(notificationType)}', which is "
            + "abstract or an interface. Notifications are published by their exact runtime type, so this handler "
            + "could never run; register a handler for each concrete notification type.");
    }

    /// <summary>
    /// Formats a type for a message.
    /// </summary>
    /// <param name="type">The type to format.</param>
    /// <returns>The formatted type name.</returns>
    private static string Name(Type type) => TypeNameFormatter.Format(type);
}
