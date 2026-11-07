using System.Diagnostics.CodeAnalysis;
using Arbitrator.Dispatch;

namespace Arbitrator.Registration;

/// <summary>
/// The handlers planned for one notification type, in registration order, while registrations are still being
/// added.
/// </summary>
internal sealed class NotificationPlan
{
    private readonly NotificationInvoker _invokerTemplate;
    private readonly List<Type> _handlerTypes;

    /// <summary>
    /// Creates a plan with no handlers for a notification type.
    /// </summary>
    /// <param name="notificationType">The notification type.</param>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    public NotificationPlan(Type notificationType)
    {
        NotificationType = notificationType;
        _invokerTemplate = NotificationInvoker.CreateTemplate(notificationType);
        _handlerTypes = [];
    }

    /// <summary>
    /// Creates an independent copy of another plan.
    /// </summary>
    /// <param name="source">The plan to copy.</param>
    private NotificationPlan(NotificationPlan source)
    {
        NotificationType = source.NotificationType;
        _invokerTemplate = source._invokerTemplate;
        _handlerTypes = [.. source._handlerTypes];
    }

    /// <summary>
    /// Gets the notification type the plan publishes.
    /// </summary>
    public Type NotificationType { get; }

    /// <summary>
    /// Adds a handler that runs after every handler already in the plan.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    public void Append(Type handlerType) => _handlerTypes.Add(handlerType);

    /// <summary>
    /// Creates an independent copy of the plan, whose handlers can change without affecting this plan.
    /// </summary>
    /// <returns>The copy.</returns>
    public NotificationPlan Copy() => new(this);

    /// <summary>
    /// Creates the invoker that runs the planned handlers.
    /// </summary>
    /// <returns>The invoker.</returns>
    public NotificationInvoker CreateInvoker() => _invokerTemplate.WithHandlers([.. _handlerTypes]);
}
