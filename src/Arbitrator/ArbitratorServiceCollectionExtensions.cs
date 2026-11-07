using Arbitrator;
using Arbitrator.Dispatch;
using Arbitrator.Registration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers Arbitrator with a service collection.
/// </summary>
public static class ArbitratorServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds Arbitrator's services and the handlers and behaviors registered by <paramref name="configure"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="ISender"/> and <see cref="IPublisher"/> are registered as scoped services: resolve them from the
        /// scope whose services the handlers and behaviors should use. The method may be called more than once on the
        /// same collection; every call adds to the same registrations, and behavior and notification handler order
        /// follows the order of registration calls. A collection whose descriptors were copied from another
        /// collection receives its own registrations on its first call, so later registrations on either collection
        /// do not affect the other.
        /// </remarks>
        /// <param name="configure">Registers handlers and behaviors.</param>
        /// <returns>The service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArbitratorConfigurationException">A registration is invalid.</exception>
        public IServiceCollection AddArbitrator(Action<ArbitratorBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            ArbitratorRegistry registry = GetOrAddRegistry(services);
            configure(new ArbitratorBuilder(registry, services));

            return services;
        }
    }

    /// <summary>
    /// Returns the registry of a service collection, adding Arbitrator's services on the first call and giving a
    /// copied collection its own registry.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The registry that belongs to <paramref name="services"/>.</returns>
    private static ArbitratorRegistry GetOrAddRegistry(IServiceCollection services)
    {
        ArbitratorRegistry? existing = FindRegistry(services);

        if (existing is null)
        {
            return AddCoreServices(services);
        }

        return existing.IsOwnedBy(services) ? existing : AdoptCopiedRegistry(services, existing);
    }

    /// <summary>
    /// Finds the registry among the descriptors of a service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The registry, or <see langword="null"/> when Arbitrator has not been added.</returns>
    private static ArbitratorRegistry? FindRegistry(IServiceCollection services)
    {
        return services
            .Where(descriptor => !descriptor.IsKeyedService && descriptor.ServiceType == typeof(ArbitratorRegistry))
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<ArbitratorRegistry>()
            .FirstOrDefault();
    }

    /// <summary>
    /// Adds the registry, the dispatch table, and the scoped sender and publisher on the first call.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The new registry.</returns>
    private static ArbitratorRegistry AddCoreServices(IServiceCollection services)
    {
        var registry = new ArbitratorRegistry(services);

        services.AddSingleton(registry);
        services.AddSingleton(static provider => DispatchTable.Create(
            provider.GetRequiredService<ArbitratorRegistry>().Freeze(),
            provider.GetService<IServiceProviderIsKeyedService>()));
        services.AddScoped<ISender, Sender>();
        services.AddScoped<IPublisher, Publisher>();

        return registry;
    }

    /// <summary>
    /// Replaces a registry that was copied from another collection with an independent copy owned by this one.
    /// </summary>
    /// <param name="services">The service collection that holds the copied registry.</param>
    /// <param name="copied">The registry that belongs to another collection.</param>
    /// <returns>The registry that now belongs to <paramref name="services"/>.</returns>
    private static ArbitratorRegistry AdoptCopiedRegistry(IServiceCollection services, ArbitratorRegistry copied)
    {
        ArbitratorRegistry registry = copied.CopyFor(services);
        services.Replace(ServiceDescriptor.Singleton(registry));
        return registry;
    }
}
