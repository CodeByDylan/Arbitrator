using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Arbitrator.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator;

/// <summary>
/// Registers handlers and pipeline behaviors with Arbitrator.
/// </summary>
/// <remarks>
/// Each call either completes entirely or changes nothing. Behaviors run in the order they are registered across
/// all <c>AddArbitrator</c> calls on a service collection: the first registered behavior is the outermost. The order
/// of handler registrations relative to behavior registrations does not matter. Notification handlers run in the
/// order they are registered across all <c>AddArbitrator</c> calls. Handlers, notification handlers, and behaviors
/// are transient.
/// </remarks>
public sealed class ArbitratorBuilder
{
    private readonly ArbitratorRegistry _registry;
    private readonly IServiceCollection _services;

    /// <summary>
    /// Creates a builder that records registrations in a registry and adds their services to a service collection.
    /// </summary>
    /// <param name="registry">The registry of the service collection.</param>
    /// <param name="services">The service collection.</param>
    internal ArbitratorBuilder(ArbitratorRegistry registry, IServiceCollection services)
    {
        _registry = registry;
        _services = services;
    }

    /// <summary>
    /// Registers a handler for every handler contract it implements.
    /// </summary>
    /// <typeparam name="THandler">
    /// A concrete class implementing <see cref="IRequestHandler{TRequest, TResponse}"/> or
    /// <see cref="IRequestHandler{TRequest}"/> one or more times.
    /// </typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// The handler type is abstract, implements no handler contract, is already registered, handles an abstract
    /// request type or a request type with more than one request contract, handles a request type that already has a
    /// handler, or makes a behavior run twice; or a service provider has attempted to resolve
    /// <see cref="ISender"/> or <see cref="IPublisher"/>; or the service collection is read-only.
    /// </exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public ArbitratorBuilder AddHandler<
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] THandler>()
        where THandler : class
    {
        EnsureServicesAreWritable();
        AddServices(_registry.RegisterHandler(typeof(THandler)));
        return this;
    }

    /// <summary>
    /// Registers a closed behavior at the next position in the pipeline order. It applies to every behavior contract
    /// it implements.
    /// </summary>
    /// <typeparam name="TBehavior">
    /// A concrete class implementing <see cref="IPipelineBehavior{TRequest, TResponse}"/> one or more times.
    /// </typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// The behavior type is abstract, implements no behavior contract, is already registered, or would run twice for
    /// a request; or a service provider has attempted to resolve <see cref="ISender"/> or <see cref="IPublisher"/>; or
    /// the service collection is read-only.
    /// </exception>
    public ArbitratorBuilder AddBehavior<
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] TBehavior>()
        where TBehavior : class
    {
        EnsureServicesAreWritable();
        AddServices(_registry.RegisterClosedBehavior(typeof(TBehavior)));
        return this;
    }

    /// <summary>
    /// Registers an open generic behavior at the next position in the pipeline order. It applies to every request
    /// whose request and response types satisfy the runtime generic constraints of the behavior.
    /// </summary>
    /// <param name="openBehaviorType">
    /// A generic type definition with two type parameters, such as <c>typeof(LoggingBehavior&lt;,&gt;)</c>, that
    /// implements <see cref="IPipelineBehavior{TRequest, TResponse}"/> with those parameters in order.
    /// </param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="openBehaviorType"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArbitratorConfigurationException">
    /// The type is not a suitable generic type definition, is already registered, or would run twice for a request;
    /// or a service provider has attempted to resolve <see cref="ISender"/> or <see cref="IPublisher"/>; or the service
    /// collection is read-only.
    /// </exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public ArbitratorBuilder AddOpenBehavior(
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] Type openBehaviorType)
    {
        ArgumentNullException.ThrowIfNull(openBehaviorType);
        EnsureServicesAreWritable();
        AddServices(_registry.RegisterOpenBehavior(openBehaviorType));
        return this;
    }

    /// <summary>
    /// Registers a notification handler for every notification contract it implements. For each notification type,
    /// it runs after every handler registered before it.
    /// </summary>
    /// <typeparam name="THandler">
    /// A concrete class implementing <see cref="INotificationHandler{TNotification}"/> one or more times.
    /// </typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// The handler type is abstract, implements no notification handler contract, is already registered as a
    /// notification handler, or handles an abstract notification type; or a service provider has attempted to
    /// resolve <see cref="ISender"/> or <see cref="IPublisher"/>; or the service collection is read-only.
    /// </exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    public ArbitratorBuilder AddNotificationHandler<
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] THandler>()
        where THandler : class
    {
        EnsureServicesAreWritable();
        AddServices(_registry.RegisterNotificationHandler(typeof(THandler)));
        return this;
    }

    /// <summary>
    /// Registers every request handler and notification handler class that an assembly defines.
    /// </summary>
    /// <remarks>
    /// Every concrete, non-generic class implementing a handler contract is registered, whatever its visibility, in
    /// ordinal order of full type names. Behaviors are never registered by a scan.
    /// </remarks>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArbitratorConfigurationException">
    /// Some types of the assembly, or the attributes of a handler type, cannot be loaded, or it defines no handler
    /// types; or a handler type found is invalid or already registered, as <see cref="AddHandler{THandler}"/> and
    /// <see cref="AddNotificationHandler{THandler}"/> report it; or a service provider has attempted to resolve
    /// <see cref="ISender"/> or <see cref="IPublisher"/>; or the service collection is read-only. Nothing is
    /// registered.
    /// </exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    public ArbitratorBuilder AddHandlersFromAssembly(Assembly assembly)
    {
        return AddHandlersFromAssembly(assembly, static _ => true);
    }

    /// <summary>
    /// Registers the request handler and notification handler classes of an assembly that a filter accepts.
    /// </summary>
    /// <remarks>
    /// The filter sees each handler type once, in ordinal order of full type names, and its exceptions propagate
    /// unchanged. Accepted types are registered in that order. Behaviors are never registered by a scan.
    /// </remarks>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="filter">Returns <see langword="true"/> for each handler type to register.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="assembly"/> or <paramref name="filter"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArbitratorConfigurationException">
    /// As for <see cref="AddHandlersFromAssembly(Assembly)"/>, or the filter excludes every handler type.
    /// </exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    public ArbitratorBuilder AddHandlersFromAssembly(Assembly assembly, Func<Type, bool> filter)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(filter);
        EnsureServicesAreWritable();

        DiscoveredHandlers handlers = HandlerAssemblyScanner.Discover(assembly, filter);
        AddServices(_registry.RegisterDiscoveredHandlers(handlers));
        return this;
    }

    /// <summary>
    /// Registers every request handler and notification handler class of the assembly that defines a marker type.
    /// </summary>
    /// <typeparam name="TMarker">Any type defined in the assembly to scan.</typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// See <see cref="AddHandlersFromAssembly(Assembly)"/>.
    /// </exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    public ArbitratorBuilder AddHandlersFromAssemblyContaining<TMarker>()
    {
        return AddHandlersFromAssembly(typeof(TMarker).Assembly);
    }

    /// <summary>
    /// Registers the handler classes a filter accepts from the assembly that defines a marker type.
    /// </summary>
    /// <typeparam name="TMarker">Any type defined in the assembly to scan.</typeparam>
    /// <param name="filter">Returns <see langword="true"/> for each handler type to register.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArbitratorConfigurationException">
    /// See <see cref="AddHandlersFromAssembly(Assembly, Func{Type, bool})"/>.
    /// </exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    public ArbitratorBuilder AddHandlersFromAssemblyContaining<TMarker>(Func<Type, bool> filter)
    {
        return AddHandlersFromAssembly(typeof(TMarker).Assembly, filter);
    }

    /// <summary>
    /// Rejects a registration before it changes anything when the service collection can no longer change.
    /// </summary>
    /// <exception cref="ArbitratorConfigurationException">The service collection is read-only.</exception>
    private void EnsureServicesAreWritable()
    {
        if (_services.IsReadOnly)
        {
            throw RegistrationErrors.ServiceCollectionIsReadOnly();
        }
    }

    /// <summary>
    /// Adds the descriptors produced by a registration to the service collection.
    /// </summary>
    /// <param name="descriptors">The descriptors to add.</param>
    private void AddServices(IReadOnlyList<ServiceDescriptor> descriptors)
    {
        foreach (ServiceDescriptor descriptor in descriptors)
        {
            _services.Add(descriptor);
        }
    }
}
