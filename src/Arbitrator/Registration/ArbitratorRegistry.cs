using System.Diagnostics.CodeAnalysis;
using Arbitrator.Dispatch;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Registration;

/// <summary>
/// Collects the handler, behavior, and notification handler registrations of one service collection, validates
/// them, and produces the dispatch maps once registration is complete.
/// </summary>
/// <remarks>
/// Each registration either completes entirely or changes nothing. Building the dispatch table freezes the registry.
/// </remarks>
internal sealed class ArbitratorRegistry
{
    private readonly Lock _gate = new();
    private readonly WeakReference<IServiceCollection> _owner;
    private Registrations _registrations;
    private bool _isFrozen;
    private DispatchMaps? _dispatchMaps;

    /// <summary>
    /// Creates an empty registry for a service collection.
    /// </summary>
    /// <param name="owner">The service collection the registry belongs to.</param>
    public ArbitratorRegistry(IServiceCollection owner)
        : this(owner, new Registrations())
    {
    }

    /// <summary>
    /// Creates a registry for a service collection from existing registrations.
    /// </summary>
    /// <param name="owner">The service collection the registry belongs to.</param>
    /// <param name="registrations">The existing registrations.</param>
    private ArbitratorRegistry(IServiceCollection owner, Registrations registrations)
    {
        _owner = new WeakReference<IServiceCollection>(owner);
        _registrations = registrations;
    }

    /// <summary>
    /// Gets the registered request pipelines.
    /// </summary>
    private RegisteredPipelines Pipelines => _registrations.Pipelines;

    /// <summary>
    /// Gets the registered behaviors.
    /// </summary>
    private RegisteredBehaviors Behaviors => _registrations.Behaviors;

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
            return new ArbitratorRegistry(owner, _registrations.Copy());
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
            return _registrations.RegisterHandler(handlerType);
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
            Behaviors.EnsureTypeIsNew(behaviorType);
            ClosedBehaviorRegistration registration =
                BehaviorTypeInspector.InspectClosed(behaviorType, Behaviors.NextPosition);

            _registrations.AppendBehavior(registration, Pipelines.PlaceClosedBehavior(registration));
            return [ComponentDescriptors.ForRequestComponent(behaviorType)];
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
            Behaviors.EnsureTypeIsNew(behaviorType);
            OpenBehaviorRegistration registration =
                BehaviorTypeInspector.InspectOpen(behaviorType, Behaviors.NextPosition);
            List<BehaviorPlacement> placements = Pipelines.PlaceOpenBehavior(registration);

            _registrations.AppendBehavior(registration, placements);
            return
            [
                .. placements.Select(
                    placement => ComponentDescriptors.ForRequestComponent(placement.Behavior.ClosedType)),
            ];
        }
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
        lock (_gate)
        {
            EnsureNotFrozen();
            return _registrations.RegisterNotificationHandler(handlerType);
        }
    }

    /// <summary>
    /// Registers handlers discovered in an assembly as one unit: either every handler is registered, or none is.
    /// </summary>
    /// <param name="handlers">The discovered handlers.</param>
    /// <returns>The descriptors for every registered handler and for the open behaviors that now apply.</returns>
    /// <exception cref="ArbitratorConfigurationException">A registration is invalid.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    public IReadOnlyList<ServiceDescriptor> RegisterDiscoveredHandlers(DiscoveredHandlers handlers)
    {
        lock (_gate)
        {
            EnsureNotFrozen();
            Registrations candidate = _registrations.Copy();
            List<ServiceDescriptor> descriptors = [];

            foreach (Type handlerType in handlers.RequestHandlerTypes)
            {
                descriptors.AddRange(candidate.RegisterHandler(handlerType));
            }

            foreach (Type handlerType in handlers.NotificationHandlerTypes)
            {
                descriptors.AddRange(candidate.RegisterNotificationHandler(handlerType));
            }

            _registrations = candidate;
            return descriptors;
        }
    }

    /// <summary>
    /// Freezes the registry and returns the invokers for every registered request and notification type, with their
    /// description.
    /// </summary>
    /// <returns>The request and notification invokers, and their description.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// A closed behavior applies to no registered request.
    /// </exception>
    public DispatchMaps Freeze()
    {
        lock (_gate)
        {
            _isFrozen = true;
            return _dispatchMaps ??= _registrations.CreateDispatchMaps();
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
}
