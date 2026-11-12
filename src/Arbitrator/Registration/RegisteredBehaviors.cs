using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Registration;

/// <summary>
/// The behaviors registered with one service collection, in global registration order.
/// </summary>
internal sealed class RegisteredBehaviors
{
    private readonly List<BehaviorRegistration> _registrations = [];

    /// <summary>
    /// Gets the registration position that the next behavior receives.
    /// </summary>
    public int NextPosition => _registrations.Count + 1;

    /// <summary>
    /// Gets the behaviors registered as closed types.
    /// </summary>
    public IEnumerable<ClosedBehaviorRegistration> ClosedRegistrations =>
        _registrations.OfType<ClosedBehaviorRegistration>();

    /// <summary>
    /// Rejects a behavior type that is already registered.
    /// </summary>
    /// <param name="behaviorType">The behavior type.</param>
    /// <exception cref="ArbitratorConfigurationException">The behavior type is already registered.</exception>
    public void EnsureTypeIsNew(Type behaviorType)
    {
        BehaviorRegistration? existing = _registrations.Find(
            registration => registration.BehaviorType == behaviorType);

        if (existing is not null)
        {
            throw BehaviorRegistrationErrors.DuplicateBehavior(existing);
        }
    }

    /// <summary>
    /// Records a behavior registration as the last in registration order.
    /// </summary>
    /// <param name="registration">The behavior registration.</param>
    public void Add(BehaviorRegistration registration) => _registrations.Add(registration);

    /// <summary>
    /// Creates an independent copy of the registered behaviors.
    /// </summary>
    /// <returns>The copy.</returns>
    public RegisteredBehaviors Copy()
    {
        var copy = new RegisteredBehaviors();
        copy._registrations.AddRange(_registrations);
        return copy;
    }

    /// <summary>
    /// Plans the pipeline of a new handler contract from the behaviors registered so far.
    /// </summary>
    /// <param name="contract">The handler contract.</param>
    /// <param name="handlerLifetime">The lifetime the handler is registered with.</param>
    /// <returns>The pipeline plan.</returns>
    /// <exception cref="ArbitratorConfigurationException">A behavior would run twice in the pipeline.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public PipelinePlan PlanPipeline(HandlerContract contract, ServiceLifetime handlerLifetime)
    {
        var plan = new PipelinePlan(contract, handlerLifetime);
        AppendAll(plan, PlanFor(contract));
        return plan;
    }

    /// <summary>
    /// Plans a new notification type from the behaviors registered so far, before any handler is added to it.
    /// </summary>
    /// <param name="notificationType">The notification type.</param>
    /// <returns>The notification plan.</returns>
    /// <exception cref="ArbitratorConfigurationException">A behavior would run twice around its handlers.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public NotificationPlan PlanNotification(Type notificationType)
    {
        var plan = new NotificationPlan(notificationType);
        AppendAll(plan, PlanForNotification(notificationType));
        return plan;
    }

    /// <summary>
    /// Finds the closed behavior types that apply to a handler contract, outermost first.
    /// </summary>
    /// <param name="contract">The handler contract.</param>
    /// <returns>The applicable behaviors, in registration order.</returns>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public IEnumerable<PlannedBehavior> PlanFor(HandlerContract contract)
    {
        foreach (BehaviorRegistration registration in _registrations)
        {
            if (CloseOver(registration, contract.Types) is { } closedType)
            {
                yield return new PlannedBehavior(closedType, registration);
            }
        }
    }

    /// <summary>
    /// Finds the closed behavior types that wrap the handlers of a notification type, outermost first.
    /// </summary>
    /// <param name="notificationType">The notification type.</param>
    /// <returns>The applicable behaviors, in registration order.</returns>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public IEnumerable<PlannedBehavior> PlanForNotification(Type notificationType)
    {
        foreach (BehaviorRegistration registration in _registrations)
        {
            if (CloseOverNotification(registration, notificationType) is { } closedType)
            {
                yield return new PlannedBehavior(closedType, registration);
            }
        }
    }

    /// <summary>
    /// Appends behaviors to a plan in order.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="behaviors">The behaviors, outermost first.</param>
    /// <exception cref="ArbitratorConfigurationException">A behavior would run twice in the plan.</exception>
    private static void AppendAll(IBehaviorPlan plan, IEnumerable<PlannedBehavior> behaviors)
    {
        foreach (PlannedBehavior behavior in behaviors)
        {
            plan.Append(behavior);
        }
    }

    /// <summary>
    /// Finds the closed behavior type a registration contributes to a request's pipeline.
    /// </summary>
    /// <param name="registration">The behavior registration.</param>
    /// <param name="types">The request and response types.</param>
    /// <returns>The closed behavior type, or <see langword="null"/> when the behavior does not apply.</returns>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    private static Type? CloseOver(BehaviorRegistration registration, RequestResponseTypes types) => registration switch
    {
        ClosedBehaviorRegistration closed => closed.AppliesTo(types) ? closed.BehaviorType : null,
        OpenBehaviorRegistration open => open.CloseOver(types),
        OpenNotificationBehaviorRegistration => null,
    };

    /// <summary>
    /// Finds the closed behavior type a registration contributes around a notification type's handlers.
    /// </summary>
    /// <param name="registration">The behavior registration.</param>
    /// <param name="notificationType">The notification type.</param>
    /// <returns>The closed behavior type, or <see langword="null"/> when the behavior does not apply.</returns>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    private static Type? CloseOverNotification(BehaviorRegistration registration, Type notificationType) =>
        registration switch
        {
            ClosedBehaviorRegistration closed =>
                closed.AppliesToNotification(notificationType) ? closed.BehaviorType : null,
            OpenBehaviorRegistration => null,
            OpenNotificationBehaviorRegistration open => open.CloseOver(notificationType),
        };
}
