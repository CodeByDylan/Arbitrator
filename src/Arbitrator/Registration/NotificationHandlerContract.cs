namespace Arbitrator.Registration;

/// <summary>
/// One notification type and a handler type registered for it.
/// </summary>
/// <param name="NotificationType">The concrete notification type.</param>
/// <param name="HandlerType">The handler implementation type.</param>
internal sealed record NotificationHandlerContract(Type NotificationType, Type HandlerType);
