using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Arbitrator.Dispatch;

namespace Arbitrator.Registration;

/// <summary>
/// The notification handlers registered with one service collection, in global registration order, and the behaviors
/// planned around them.
/// </summary>
internal sealed class RegisteredNotificationHandlers
{
    private readonly HashSet<Type> _handlerTypes = [];
    private readonly Dictionary<Type, NotificationPlan> _plansByNotificationType = [];

    /// <summary>
    /// Rejects a notification handler type that is already registered.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <exception cref="ArbitratorConfigurationException">The handler type is already registered.</exception>
    public void EnsureHandlerTypeIsNew(Type handlerType)
    {
        if (_handlerTypes.Contains(handlerType))
        {
            throw NotificationRegistrationErrors.DuplicateHandlerType(handlerType);
        }
    }

    /// <summary>
    /// Finds the notification types among a handler's contracts that have no plan yet.
    /// </summary>
    /// <param name="contracts">The notification contracts the handler type implements.</param>
    /// <returns>The notification types without a plan.</returns>
    public IEnumerable<Type> FindUnplannedTypes(IReadOnlyList<NotificationHandlerContract> contracts)
    {
        return contracts
            .Select(contract => contract.NotificationType)
            .Where(notificationType => !_plansByNotificationType.ContainsKey(notificationType));
    }

    /// <summary>
    /// Records a handler type as the last handler of every notification type it handles.
    /// </summary>
    /// <param name="handler">The handler type and the lifetime it is registered with.</param>
    /// <param name="contracts">The notification contracts the handler type implements.</param>
    /// <param name="newPlans">
    /// The plans for the notification types that had none, already holding their behaviors.
    /// </param>
    public void Add(
        ComponentRegistration handler,
        IReadOnlyList<NotificationHandlerContract> contracts,
        IReadOnlyList<NotificationPlan> newPlans)
    {
        foreach (NotificationPlan plan in newPlans)
        {
            _plansByNotificationType.Add(plan.NotificationType, plan);
        }

        _handlerTypes.Add(handler.ImplementationType);

        foreach (NotificationHandlerContract contract in contracts)
        {
            _plansByNotificationType[contract.NotificationType].AppendHandler(handler);
        }
    }

    /// <summary>
    /// Finds where a closed behavior goes: at the end of every plan whose notification type it applies to.
    /// </summary>
    /// <param name="registration">The closed behavior registration.</param>
    /// <returns>The placements.</returns>
    public IEnumerable<BehaviorPlacement> PlaceClosedBehavior(ClosedBehaviorRegistration registration)
    {
        var plannedBehavior = new PlannedBehavior(registration.BehaviorType, registration);

        return _plansByNotificationType.Values
            .Where(plan => registration.AppliesToNotification(plan.NotificationType))
            .Select(plan => new BehaviorPlacement(plan, plannedBehavior));
    }

    /// <summary>
    /// Finds where an open notification behavior goes: closed over the notification type of every plan whose type
    /// satisfies the behavior's generic constraints.
    /// </summary>
    /// <param name="registration">The open notification behavior registration.</param>
    /// <returns>The placements, each with its closed behavior type.</returns>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public List<BehaviorPlacement> PlaceOpenBehavior(OpenNotificationBehaviorRegistration registration)
    {
        List<BehaviorPlacement> placements = [];

        foreach (NotificationPlan plan in _plansByNotificationType.Values)
        {
            if (registration.CloseOver(plan.NotificationType) is { } closedType)
            {
                placements.Add(new BehaviorPlacement(plan, new PlannedBehavior(closedType, registration)));
            }
        }

        return placements;
    }

    /// <summary>
    /// Determines whether a behavior registration wraps the handlers of any notification type.
    /// </summary>
    /// <param name="registration">The behavior registration.</param>
    /// <returns><see langword="true"/> when some plan contains the registration.</returns>
    public bool Contains(BehaviorRegistration registration)
    {
        return _plansByNotificationType.Values.Any(plan => plan.Contains(registration));
    }

    /// <summary>
    /// Describes every notification type that has handlers, in no particular order.
    /// </summary>
    /// <returns>One description per notification type.</returns>
    public IEnumerable<NotificationRegistration> Describe()
    {
        return _plansByNotificationType.Values.Select(plan => plan.Describe());
    }

    /// <summary>
    /// Creates the invoker for every notification type that has handlers.
    /// </summary>
    /// <returns>The invokers, keyed by notification type.</returns>
    public FrozenDictionary<Type, NotificationInvoker> CreateInvokers()
    {
        return _plansByNotificationType.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.CreateInvoker());
    }

    /// <summary>
    /// Creates an independent copy of the registered notification handlers and their plans.
    /// </summary>
    /// <returns>The copy.</returns>
    public RegisteredNotificationHandlers Copy()
    {
        var copy = new RegisteredNotificationHandlers();
        copy._handlerTypes.UnionWith(_handlerTypes);

        foreach ((Type notificationType, NotificationPlan plan) in _plansByNotificationType)
        {
            copy._plansByNotificationType.Add(notificationType, plan.Copy());
        }

        return copy;
    }
}
