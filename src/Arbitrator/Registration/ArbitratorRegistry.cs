using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Arbitrator.Dispatch;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Registration;

/// <summary>
/// Collects the handler and behavior registrations of one service collection, validates them, and produces the
/// dispatch table once registration is complete.
/// </summary>
/// <remarks>
/// Each registration either completes entirely or changes nothing. Building the dispatch table freezes the registry.
/// </remarks>
internal sealed class ArbitratorRegistry
{
    private readonly Lock _gate = new();
    private readonly WeakReference<IServiceCollection> _owner;
    private readonly RegisteredPipelines _pipelines;
    private readonly RegisteredBehaviors _behaviors;
    private bool _isFrozen;
    private FrozenDictionary<Type, RequestInvoker>? _invokersByRequestType;

    /// <summary>
    /// Creates an empty registry for a service collection.
    /// </summary>
    /// <param name="owner">The service collection the registry belongs to.</param>
    public ArbitratorRegistry(IServiceCollection owner)
        : this(owner, new RegisteredPipelines(), new RegisteredBehaviors())
    {
    }

    /// <summary>
    /// Creates a registry for a service collection from existing registrations.
    /// </summary>
    /// <param name="owner">The service collection the registry belongs to.</param>
    /// <param name="pipelines">The registered pipelines.</param>
    /// <param name="behaviors">The registered behaviors.</param>
    private ArbitratorRegistry(IServiceCollection owner, RegisteredPipelines pipelines, RegisteredBehaviors behaviors)
    {
        _owner = new WeakReference<IServiceCollection>(owner);
        _pipelines = pipelines;
        _behaviors = behaviors;
    }

    /// <summary>
    /// Determines whether the registry belongs to a service collection, rather than having been copied into it
    /// together with the descriptors of another collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns><see langword="true"/> when the registry was created for <paramref name="services"/>.</returns>
    public bool IsOwnedBy(IServiceCollection services)
    {
        return _owner.TryGetTarget(out IServiceCollection? owner) && ReferenceEquals(owner, services);
    }

    /// <summary>
    /// Creates an independent, unfrozen registry for another service collection with the same registrations.
    /// </summary>
    /// <param name="owner">The service collection the copy belongs to.</param>
    /// <returns>The copy.</returns>
    public ArbitratorRegistry CopyFor(IServiceCollection owner)
    {
        lock (_gate)
        {
            return new ArbitratorRegistry(owner, _pipelines.Copy(), _behaviors.Copy());
        }
    }

    /// <summary>
    /// Registers a handler type for every handler contract it implements.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <returns>The descriptors for the handler and for the open behaviors that now apply to its requests.</returns>
    /// <exception cref="ArbitratorConfigurationException">The registration is invalid.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public IReadOnlyList<ServiceDescriptor> RegisterHandler(
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] Type handlerType)
    {
        lock (_gate)
        {
            EnsureNotFrozen();
            _pipelines.EnsureHandlerTypeIsNew(handlerType);
            IReadOnlyList<HandlerContract> contracts = HandlerTypeInspector.GetContracts(handlerType);
            _pipelines.EnsureRequestTypesHaveNoHandler(contracts);
            PipelinePlan[] plans = [.. contracts.Select(PlanPipeline)];

            _pipelines.AddRange(plans);
            return [ComponentDescriptors.Create(handlerType), .. plans.SelectMany(CreateOpenBehaviorDescriptors)];
        }
    }

    /// <summary>
    /// Registers a closed behavior at the next registration position.
    /// </summary>
    /// <param name="behaviorType">The closed behavior type.</param>
    /// <returns>The descriptor for the behavior.</returns>
    /// <exception cref="ArbitratorConfigurationException">The registration is invalid.</exception>
    public IReadOnlyList<ServiceDescriptor> RegisterClosedBehavior(
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] Type behaviorType)
    {
        lock (_gate)
        {
            EnsureNotFrozen();
            _behaviors.EnsureTypeIsNew(behaviorType);
            ClosedBehaviorRegistration registration =
                BehaviorTypeInspector.InspectClosed(behaviorType, _behaviors.NextPosition);

            AppendBehavior(registration, _pipelines.PlaceClosedBehavior(registration));
            return [ComponentDescriptors.Create(behaviorType)];
        }
    }

    /// <summary>
    /// Registers an open behavior at the next registration position.
    /// </summary>
    /// <param name="behaviorType">The generic type definition of the behavior.</param>
    /// <returns>The descriptors for the closed behavior types that apply to registered requests.</returns>
    /// <exception cref="ArbitratorConfigurationException">The registration is invalid.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public IReadOnlyList<ServiceDescriptor> RegisterOpenBehavior(
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] Type behaviorType)
    {
        lock (_gate)
        {
            EnsureNotFrozen();
            _behaviors.EnsureTypeIsNew(behaviorType);
            OpenBehaviorRegistration registration =
                BehaviorTypeInspector.InspectOpen(behaviorType, _behaviors.NextPosition);
            List<BehaviorPlacement> placements = _pipelines.PlaceOpenBehavior(registration);

            AppendBehavior(registration, placements);
            return [.. placements.Select(placement => ComponentDescriptors.Create(placement.Behavior.ClosedType))];
        }
    }

    /// <summary>
    /// Freezes the registry and returns the invoker for each registered request type.
    /// </summary>
    /// <returns>The invokers, keyed by request type.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// A closed behavior applies to no registered request.
    /// </exception>
    public FrozenDictionary<Type, RequestInvoker> Freeze()
    {
        lock (_gate)
        {
            _isFrozen = true;
            _pipelines.EnsureEveryClosedBehaviorApplies(_behaviors.ClosedRegistrations);

            return _invokersByRequestType ??= _pipelines.CreateInvokers();
        }
    }

    /// <summary>
    /// Rejects registrations after the dispatch table has been built.
    /// </summary>
    /// <exception cref="ArbitratorConfigurationException">The registry is frozen.</exception>
    private void EnsureNotFrozen()
    {
        if (_isFrozen)
        {
            throw RegistrationErrors.RegistrationFrozen();
        }
    }

    /// <summary>
    /// Plans the pipeline of a new handler contract from the behaviors registered so far.
    /// </summary>
    /// <param name="contract">The handler contract.</param>
    /// <returns>The pipeline plan.</returns>
    /// <exception cref="ArbitratorConfigurationException">A behavior would run twice in the pipeline.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    private PipelinePlan PlanPipeline(HandlerContract contract)
    {
        var plan = new PipelinePlan(contract);

        foreach (PlannedBehavior behavior in _behaviors.PlanFor(contract))
        {
            plan.Append(behavior);
        }

        return plan;
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
            .Select(behavior => ComponentDescriptors.Create(behavior.ClosedType));
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
    private void AppendBehavior(BehaviorRegistration registration, IReadOnlyList<BehaviorPlacement> placements)
    {
        foreach (BehaviorPlacement placement in placements)
        {
            placement.Plan.EnsureCanAppend(placement.Behavior);
        }

        _behaviors.Add(registration);

        foreach (BehaviorPlacement placement in placements)
        {
            placement.Plan.Append(placement.Behavior);
        }
    }
}
