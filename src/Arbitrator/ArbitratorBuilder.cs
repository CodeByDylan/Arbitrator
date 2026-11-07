using System.Diagnostics.CodeAnalysis;
using Arbitrator.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator;

/// <summary>
/// Registers handlers and pipeline behaviors with Arbitrator.
/// </summary>
/// <remarks>
/// Each call either completes entirely or changes nothing. Behaviors run in the order they are registered across
/// all <c>AddArbitrator</c> calls on a service collection: the first registered behavior is the outermost. The order
/// of handler registrations relative to behavior registrations does not matter. Handlers and behaviors are
/// transient.
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
    /// <see cref="ISender"/>; or the service collection is read-only.
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
    /// a request; or a service provider has attempted to resolve <see cref="ISender"/>; or the service collection is
    /// read-only.
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
    /// or a service provider has attempted to resolve <see cref="ISender"/>; or the service collection is read-only.
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
