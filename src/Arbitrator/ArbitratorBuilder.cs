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
/// of handler registrations relative to behavior registrations does not matter. Notification handlers run in the
/// order they are registered across all <c>AddArbitrator</c> calls. Handlers, notification handlers, and behaviors
/// are transient unless they are registered with another <see cref="ServiceLifetime"/>.
/// </remarks>
public sealed partial class ArbitratorBuilder
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
    /// Registers a transient handler for every handler contract it implements.
    /// </summary>
    /// <typeparam name="THandler">
    /// A concrete class implementing <see cref="IRequestHandler{TRequest, TResponse}"/>,
    /// <see cref="IRequestHandler{TRequest}"/>, or <see cref="IStreamRequestHandler{TRequest, TItem}"/> one or more
    /// times.
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
        return AddHandler<THandler>(ServiceLifetime.Transient);
    }

    /// <inheritdoc cref="AddHandler{THandler}()"/>
    /// <summary>
    /// Registers a handler with a lifetime for every handler contract it implements.
    /// </summary>
    /// <param name="lifetime">
    /// The lifetime of the handler. A scoped or singleton handler is shared by every dispatch that resolves it, so it
    /// must not keep request-specific state, and a singleton must not depend on scoped services.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is not a defined value.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public ArbitratorBuilder AddHandler<
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] THandler>(ServiceLifetime lifetime)
        where THandler : class
    {
        EnsureLifetimeIsDefined(lifetime);
        EnsureServicesAreWritable();
        AddServices(_registry.RegisterHandler(typeof(THandler), lifetime));
        return this;
    }

    /// <summary>
    /// Registers a transient notification handler for every notification contract it implements. For each
    /// notification type, it runs after every handler registered before it.
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
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public ArbitratorBuilder AddNotificationHandler<
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] THandler>()
        where THandler : class
    {
        return AddNotificationHandler<THandler>(ServiceLifetime.Transient);
    }

    /// <inheritdoc cref="AddNotificationHandler{THandler}()"/>
    /// <summary>
    /// Registers a notification handler with a lifetime for every notification contract it implements. For each
    /// notification type, it runs after every handler registered before it.
    /// </summary>
    /// <param name="lifetime">
    /// The lifetime of the handler. A scoped or singleton handler is shared by every publish that resolves it, so it
    /// must not keep notification-specific state, and a singleton must not depend on scoped services.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is not a defined value.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public ArbitratorBuilder AddNotificationHandler<
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] THandler>(ServiceLifetime lifetime)
        where THandler : class
    {
        EnsureLifetimeIsDefined(lifetime);
        EnsureServicesAreWritable();
        AddServices(_registry.RegisterNotificationHandler(typeof(THandler), lifetime));
        return this;
    }

    /// <summary>
    /// Rejects a lifetime that is not one of the values <see cref="ServiceLifetime"/> defines.
    /// </summary>
    /// <param name="lifetime">The lifetime passed to a registration method.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is not a defined value.</exception>
    private static void EnsureLifetimeIsDefined(ServiceLifetime lifetime)
    {
        if (!Enum.IsDefined(lifetime))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                lifetime,
                "The lifetime must be Transient, Scoped, or Singleton.");
        }
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
