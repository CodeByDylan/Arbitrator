using System.Diagnostics.CodeAnalysis;
using Arbitrator.Dispatch;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Registration;

/// <summary>
/// The request pipelines, behaviors, and notification handlers registered with one service collection.
/// </summary>
internal sealed class Registrations
{
    /// <summary>
    /// Creates empty registrations.
    /// </summary>
    public Registrations()
        : this(new RegisteredPipelines(), new RegisteredBehaviors(), new RegisteredNotificationHandlers())
    {
    }

    /// <summary>
    /// Creates registrations from existing collections.
    /// </summary>
    /// <param name="pipelines">The registered request pipelines.</param>
    /// <param name="behaviors">The registered behaviors.</param>
    /// <param name="notificationHandlers">The registered notification handlers.</param>
    private Registrations(
        RegisteredPipelines pipelines,
        RegisteredBehaviors behaviors,
        RegisteredNotificationHandlers notificationHandlers)
    {
        Pipelines = pipelines;
        Behaviors = behaviors;
        NotificationHandlers = notificationHandlers;
    }

    /// <summary>
    /// Gets the registered request pipelines.
    /// </summary>
    public RegisteredPipelines Pipelines { get; }

    /// <summary>
    /// Gets the registered behaviors.
    /// </summary>
    public RegisteredBehaviors Behaviors { get; }

    /// <summary>
    /// Gets the registered notification handlers.
    /// </summary>
    public RegisteredNotificationHandlers NotificationHandlers { get; }

    /// <summary>
    /// Creates independent copies of all registrations.
    /// </summary>
    /// <returns>The copy.</returns>
    public Registrations Copy() => new(Pipelines.Copy(), Behaviors.Copy(), NotificationHandlers.Copy());

    /// <summary>
    /// Verifies the registrations as a whole and creates the invokers the dispatch table uses, together with their
    /// public description.
    /// </summary>
    /// <returns>The request and notification invokers, and their description.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// A closed behavior applies to no registered request or notification.
    /// </exception>
    public DispatchMaps CreateDispatchMaps()
    {
        EnsureEveryClosedBehaviorApplies();
        var description = new ArbitratorRegistrations(Pipelines.Describe(), NotificationHandlers.Describe());

        return new DispatchMaps(Pipelines.CreateInvokers(), NotificationHandlers.CreateInvokers(), description);
    }

    /// <summary>
    /// Registers a handler type for every handler contract it implements, after validating it against the
    /// registrations so far.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <returns>The descriptors for the handler and for the open behaviors that now apply to its requests.</returns>
    /// <exception cref="ArbitratorConfigurationException">The registration is invalid.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public IReadOnlyList<ServiceDescriptor> RegisterHandler(
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] Type handlerType)
    {
        Pipelines.EnsureHandlerTypeIsNew(handlerType);
        IReadOnlyList<HandlerContract> contracts = HandlerTypeInspector.GetContracts(handlerType);
        Pipelines.EnsureRequestTypesHaveNoHandler(contracts);
        PipelinePlan[] plans = [.. contracts.Select(Behaviors.PlanPipeline)];

        Pipelines.AddRange(plans);
        return
        [
            ComponentDescriptors.ForRequestComponent(handlerType),
            .. plans.SelectMany(ComponentDescriptors.ForOpenBehaviors),
        ];
    }

    /// <summary>
    /// Registers a notification handler type for every notification contract it implements, after every notification
    /// handler registered so far.
    /// </summary>
    /// <param name="handlerType">The notification handler type.</param>
    /// <returns>
    /// The descriptors for the handler and for the open behaviors that now wrap the handlers of its notification types.
    /// </returns>
    /// <exception cref="ArbitratorConfigurationException">The registration is invalid.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public IReadOnlyList<ServiceDescriptor> RegisterNotificationHandler(
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] Type handlerType)
    {
        NotificationHandlers.EnsureHandlerTypeIsNew(handlerType);
        IReadOnlyList<NotificationHandlerContract> contracts =
            NotificationHandlerTypeInspector.GetContracts(handlerType);
        NotificationPlan[] newPlans =
            [.. NotificationHandlers.FindUnplannedTypes(contracts).Select(Behaviors.PlanNotification)];

        NotificationHandlers.Add(handlerType, contracts, newPlans);
        return
        [
            ComponentDescriptors.ForNotificationHandler(handlerType),
            .. newPlans.SelectMany(ComponentDescriptors.ForOpenBehaviors),
        ];
    }

    /// <summary>
    /// Registers a closed behavior at the next registration position, around every request and notification it
    /// applies to.
    /// </summary>
    /// <param name="behaviorType">The closed behavior type.</param>
    /// <returns>The descriptors for the behavior.</returns>
    /// <exception cref="ArbitratorConfigurationException">The registration is invalid.</exception>
    public IReadOnlyList<ServiceDescriptor> RegisterClosedBehavior(
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] Type behaviorType)
    {
        Behaviors.EnsureTypeIsNew(behaviorType);
        ClosedBehaviorRegistration registration =
            BehaviorTypeInspector.InspectClosed(behaviorType, Behaviors.NextPosition);

        BehaviorPlacement[] placements =
            [.. Pipelines.PlaceClosedBehavior(registration), .. NotificationHandlers.PlaceClosedBehavior(registration)];

        AppendBehavior(registration, placements);
        return ComponentDescriptors.ForClosedBehavior(behaviorType, registration);
    }

    /// <summary>
    /// Registers an open behavior at the next registration position, around every registered request or notification
    /// whose types satisfy its generic constraints.
    /// </summary>
    /// <param name="behaviorType">The generic type definition of the behavior.</param>
    /// <returns>The descriptors for the closed behavior types that now apply.</returns>
    /// <exception cref="ArbitratorConfigurationException">The registration is invalid.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public IReadOnlyList<ServiceDescriptor> RegisterOpenBehavior(
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] Type behaviorType)
    {
        Behaviors.EnsureTypeIsNew(behaviorType);
        OpenRegistration registration = BehaviorTypeInspector.InspectOpen(behaviorType, Behaviors.NextPosition);
        List<BehaviorPlacement> placements = registration switch
        {
            OpenBehaviorRegistration open => Pipelines.PlaceOpenBehavior(open),
            OpenNotificationBehaviorRegistration open => NotificationHandlers.PlaceOpenBehavior(open),
        };

        AppendBehavior(registration, placements);
        return [.. placements.Select(ComponentDescriptors.ForPlacement)];
    }

    /// <summary>
    /// Records a behavior registration and appends it to the plans it applies to, after verifying that no plan would
    /// run the same closed behavior type twice.
    /// </summary>
    /// <param name="registration">The behavior registration.</param>
    /// <param name="placements">The plans the behavior applies to.</param>
    /// <exception cref="ArbitratorConfigurationException">A plan would run a closed behavior type twice.</exception>
    private void AppendBehavior(BehaviorRegistration registration, IReadOnlyList<BehaviorPlacement> placements)
    {
        foreach (BehaviorPlacement placement in placements)
        {
            placement.Plan.EnsureCanAppend(placement.Behavior);
        }

        Behaviors.Add(registration);

        foreach (BehaviorPlacement placement in placements)
        {
            placement.Plan.Append(placement.Behavior);
        }
    }

    /// <summary>
    /// Rejects closed behaviors that apply to no registered request and no notification with handlers.
    /// </summary>
    /// <exception cref="ArbitratorConfigurationException">A closed behavior applies to nothing registered.</exception>
    private void EnsureEveryClosedBehaviorApplies()
    {
        foreach (ClosedBehaviorRegistration registration in Behaviors.ClosedRegistrations)
        {
            if (!Pipelines.Contains(registration) && !NotificationHandlers.Contains(registration))
            {
                throw BehaviorRegistrationErrors.ClosedBehaviorWithoutHandler(registration);
            }
        }
    }
}
