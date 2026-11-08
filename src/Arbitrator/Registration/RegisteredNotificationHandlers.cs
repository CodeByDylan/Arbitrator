using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Arbitrator.Dispatch;

namespace Arbitrator.Registration;

/// <summary>
/// The notification handlers registered with one service collection, in global registration order.
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
    /// Records a handler type as the last handler of every notification type it handles.
    /// </summary>
    /// <remarks>
    /// Plans for notification types without handlers are created before anything is recorded, so a failure leaves
    /// the registrations unchanged.
    /// </remarks>
    /// <param name="handlerType">The handler type.</param>
    /// <param name="contracts">The notification contracts the handler type implements.</param>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    public void Add(Type handlerType, IReadOnlyList<NotificationHandlerContract> contracts)
    {
        List<NotificationPlan> newPlans = CreateMissingPlans(contracts);

        foreach (NotificationPlan plan in newPlans)
        {
            _plansByNotificationType.Add(plan.NotificationType, plan);
        }

        _handlerTypes.Add(handlerType);

        foreach (NotificationHandlerContract contract in contracts)
        {
            _plansByNotificationType[contract.NotificationType].Append(handlerType);
        }
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
    /// Creates an independent copy of the registered notification handlers.
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

    /// <summary>
    /// Creates plans for the notification types that have no handlers yet, without recording them.
    /// </summary>
    /// <param name="contracts">The notification contracts of a new handler.</param>
    /// <returns>The new plans.</returns>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    private List<NotificationPlan> CreateMissingPlans(IReadOnlyList<NotificationHandlerContract> contracts)
    {
        return
        [
            .. contracts
                .Select(contract => contract.NotificationType)
                .Where(notificationType => !_plansByNotificationType.ContainsKey(notificationType))
                .Select(notificationType => new NotificationPlan(notificationType)),
        ];
    }
}
