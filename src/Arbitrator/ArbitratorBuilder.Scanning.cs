using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Arbitrator.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator;

public sealed partial class ArbitratorBuilder
{
    /// <summary>
    /// Registers every request handler and notification handler class that an assembly defines, as transient.
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
    /// types; or a handler type found is invalid or already registered, as <see cref="AddHandler{THandler}()"/> and
    /// <see cref="AddNotificationHandler{THandler}()"/> report it; or a service provider has attempted to resolve
    /// <see cref="ISender"/> or <see cref="IPublisher"/>; or the service collection is read-only. Nothing is
    /// registered.
    /// </exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    public ArbitratorBuilder AddHandlersFromAssembly(Assembly assembly)
    {
        return AddHandlersFromAssembly(assembly, ServiceLifetime.Transient);
    }

    /// <inheritdoc cref="AddHandlersFromAssembly(Assembly)"/>
    /// <summary>
    /// Registers every request handler and notification handler class that an assembly defines, with a lifetime.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="lifetime">The lifetime every handler this scan registers gets.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is not a defined value.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    public ArbitratorBuilder AddHandlersFromAssembly(Assembly assembly, ServiceLifetime lifetime)
    {
        return AddHandlersFromAssembly(assembly, static _ => true, lifetime);
    }

    /// <summary>
    /// Registers the request handler and notification handler classes of an assembly that a filter accepts, as
    /// transient.
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
        return AddHandlersFromAssembly(assembly, filter, ServiceLifetime.Transient);
    }

    /// <inheritdoc cref="AddHandlersFromAssembly(Assembly, Func{Type, bool})"/>
    /// <summary>
    /// Registers the request handler and notification handler classes of an assembly that a filter accepts, with a
    /// lifetime.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="filter">Returns <see langword="true"/> for each handler type to register.</param>
    /// <param name="lifetime">The lifetime every handler this scan registers gets.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is not a defined value.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    public ArbitratorBuilder AddHandlersFromAssembly(
        Assembly assembly,
        Func<Type, bool> filter,
        ServiceLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(filter);
        EnsureLifetimeIsDefined(lifetime);
        EnsureServicesAreWritable();

        DiscoveredHandlers handlers = HandlerAssemblyScanner.Discover(assembly, filter);
        AddServices(_registry.RegisterDiscoveredHandlers(handlers, lifetime));
        return this;
    }

    /// <summary>
    /// Registers every request handler and notification handler class of the assembly that defines a marker type, as
    /// transient.
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

    /// <inheritdoc cref="AddHandlersFromAssemblyContaining{TMarker}()"/>
    /// <summary>
    /// Registers every request handler and notification handler class of the assembly that defines a marker type,
    /// with a lifetime.
    /// </summary>
    /// <param name="lifetime">The lifetime every handler this scan registers gets.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is not a defined value.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    public ArbitratorBuilder AddHandlersFromAssemblyContaining<TMarker>(ServiceLifetime lifetime)
    {
        return AddHandlersFromAssembly(typeof(TMarker).Assembly, lifetime);
    }

    /// <summary>
    /// Registers the handler classes a filter accepts from the assembly that defines a marker type, as transient.
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

    /// <inheritdoc cref="AddHandlersFromAssemblyContaining{TMarker}(Func{Type, bool})"/>
    /// <summary>
    /// Registers the handler classes a filter accepts from the assembly that defines a marker type, with a lifetime.
    /// </summary>
    /// <param name="filter">Returns <see langword="true"/> for each handler type to register.</param>
    /// <param name="lifetime">The lifetime every handler this scan registers gets.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is not a defined value.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    public ArbitratorBuilder AddHandlersFromAssemblyContaining<TMarker>(
        Func<Type, bool> filter,
        ServiceLifetime lifetime)
    {
        return AddHandlersFromAssembly(typeof(TMarker).Assembly, filter, lifetime);
    }
}
