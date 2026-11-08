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
    /// Verifies the registrations as a whole and creates the invokers the dispatch table uses.
    /// </summary>
    /// <returns>The request and notification invokers.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// A closed behavior applies to no registered request.
    /// </exception>
    public DispatchMaps CreateDispatchMaps()
    {
        Pipelines.EnsureEveryClosedBehaviorApplies(Behaviors.ClosedRegistrations);
        return new DispatchMaps(Pipelines.CreateInvokers(), NotificationHandlers.CreateInvokers());
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
        PipelinePlan[] plans = [.. contracts.Select(PlanPipeline)];

        Pipelines.AddRange(plans);
        return
        [
            ComponentDescriptors.ForRequestComponent(handlerType),
            .. plans.SelectMany(CreateOpenBehaviorDescriptors),
        ];
    }

    /// <summary>
    /// Registers a notification handler type for every notification contract it implements, after every notification
    /// handler registered so far.
    /// </summary>
    /// <param name="handlerType">The notification handler type.</param>
    /// <returns>The descriptor for the handler.</returns>
    /// <exception cref="ArbitratorConfigurationException">The registration is invalid.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    public IReadOnlyList<ServiceDescriptor> RegisterNotificationHandler(
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] Type handlerType)
    {
        NotificationHandlers.EnsureHandlerTypeIsNew(handlerType);
        IReadOnlyList<NotificationHandlerContract> contracts =
            NotificationHandlerTypeInspector.GetContracts(handlerType);

        NotificationHandlers.Add(handlerType, contracts);
        return [ComponentDescriptors.ForNotificationHandler(handlerType)];
    }

    /// <summary>
    /// Plans the pipeline of a new handler contract from the behaviors registered so far.
    /// </summary>
    /// <param name="contract">The handler contract.</param>
    /// <returns>The pipeline plan.</returns>
    /// <exception cref="ArbitratorConfigurationException">A behavior would run twice in the pipeline.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public PipelinePlan PlanPipeline(HandlerContract contract)
    {
        var plan = new PipelinePlan(contract);

        foreach (PlannedBehavior behavior in Behaviors.PlanFor(contract))
        {
            plan.Append(behavior);
        }

        return plan;
    }

    /// <summary>
    /// Records a behavior registration and appends it to the pipelines it applies to, after verifying that no
    /// pipeline would run the same closed behavior type twice.
    /// </summary>
    /// <param name="registration">The behavior registration.</param>
    /// <param name="placements">The pipelines the behavior applies to.</param>
    /// <exception cref="ArbitratorConfigurationException">
    /// A pipeline would run a closed behavior type twice.
    /// </exception>
    public void AppendBehavior(BehaviorRegistration registration, IReadOnlyList<BehaviorPlacement> placements)
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
    /// Creates the descriptors for the closed types that open behaviors contribute to a pipeline.
    /// </summary>
    /// <param name="plan">The pipeline plan.</param>
    /// <returns>The descriptors.</returns>
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    private static IEnumerable<ServiceDescriptor> CreateOpenBehaviorDescriptors(PipelinePlan plan)
    {
        return plan.Behaviors
            .Where(behavior => behavior.Registration is OpenBehaviorRegistration)
            .Select(behavior => ComponentDescriptors.ForRequestComponent(behavior.ClosedType));
    }
}
