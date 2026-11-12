using System.Diagnostics.CodeAnalysis;
using Arbitrator.Dispatch;

namespace Arbitrator.Registration;

/// <summary>
/// The handlers planned for one notification type, in registration order, and the behaviors that wrap each of them,
/// while registrations are still being added.
/// </summary>
internal sealed class NotificationPlan : IBehaviorPlan
{
    private readonly NotificationInvoker _invokerTemplate;
    private readonly List<ComponentRegistration> _handlers;
    private readonly PlannedBehaviors _behaviors;

    /// <summary>
    /// Creates a plan with no handlers and no behaviors for a notification type.
    /// </summary>
    /// <param name="notificationType">The notification type.</param>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    public NotificationPlan(Type notificationType)
    {
        NotificationType = notificationType;
        _invokerTemplate = NotificationInvoker.CreateTemplate(notificationType);
        _handlers = [];
        _behaviors = new PlannedBehaviors();
    }

    /// <summary>
    /// Creates an independent copy of another plan.
    /// </summary>
    /// <param name="source">The plan to copy.</param>
    private NotificationPlan(NotificationPlan source)
    {
        NotificationType = source.NotificationType;
        _invokerTemplate = source._invokerTemplate;
        _handlers = [.. source._handlers];
        _behaviors = source._behaviors.Copy();
    }

    /// <summary>
    /// Gets the notification type the plan publishes.
    /// </summary>
    public Type NotificationType { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PlannedBehavior> Behaviors => _behaviors.Items;

    /// <inheritdoc/>
    public ArbitratorServiceKey BehaviorServiceKey => ArbitratorServiceKey.NotificationBehaviors;

    /// <summary>
    /// Adds a handler that runs after every handler already in the plan.
    /// </summary>
    /// <param name="handler">The handler and the lifetime it is registered with.</param>
    public void AppendHandler(ComponentRegistration handler) => _handlers.Add(handler);

    /// <inheritdoc/>
    public void EnsureCanAppend(PlannedBehavior candidate)
    {
        if (_behaviors.FindSameClosedType(candidate) is { } existing)
        {
            throw BehaviorRegistrationErrors.NotificationBehaviorWouldRunTwice(new BehaviorConflict(
                candidate.ClosedType,
                NotificationType,
                existing.Registration,
                candidate.Registration));
        }
    }

    /// <inheritdoc/>
    public void Append(PlannedBehavior behavior)
    {
        EnsureCanAppend(behavior);
        _behaviors.Add(behavior);
    }

    /// <inheritdoc/>
    public bool Contains(BehaviorRegistration registration) => _behaviors.Contains(registration);

    /// <summary>
    /// Creates an independent copy of the plan, whose handlers and behaviors can change without affecting this plan.
    /// </summary>
    /// <returns>The copy.</returns>
    public NotificationPlan Copy() => new(this);

    /// <summary>
    /// Creates the invoker that runs the planned handlers, each inside its behaviors.
    /// </summary>
    /// <returns>The invoker.</returns>
    public NotificationInvoker CreateInvoker()
    {
        return _invokerTemplate.WithComponents(
            [.. _handlers.Select(handler => handler.ImplementationType)],
            _behaviors.ClosedTypes());
    }

    /// <summary>
    /// Describes the notification type, its planned handlers, and its planned behaviors.
    /// </summary>
    /// <returns>The description.</returns>
    public NotificationRegistration Describe() => new(NotificationType, [.. _handlers], _behaviors.Components());
}
