using System.Collections.Frozen;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Dispatch;

/// <summary>
/// The immutable maps from request and notification types to the invokers that dispatch them.
/// </summary>
internal sealed class DispatchTable
{
    private readonly FrozenDictionary<Type, RequestInvoker> _invokersByRequestType;
    private readonly FrozenDictionary<Type, NotificationInvoker> _invokersByNotificationType;

    /// <summary>
    /// Creates a dispatch table.
    /// </summary>
    /// <param name="maps">The request and notification invokers.</param>
    private DispatchTable(DispatchMaps maps)
    {
        _invokersByRequestType = maps.RequestInvokers;
        _invokersByNotificationType = maps.NotificationInvokers;
    }

    /// <summary>
    /// Creates the dispatch table of one service provider after verifying that the provider can resolve every
    /// handler and behavior the table refers to.
    /// </summary>
    /// <param name="maps">The request and notification invokers.</param>
    /// <param name="keyedServices">
    /// The provider's keyed-service lookup, or <see langword="null"/> when the provider does not offer one.
    /// </param>
    /// <returns>The dispatch table.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// A handler or behavior was registered after the provider was built.
    /// </exception>
    public static DispatchTable Create(DispatchMaps maps, IServiceProviderIsKeyedService? keyedServices)
    {
        if (keyedServices is not null)
        {
            EnsureComponentsAreRegistered(maps, keyedServices);
        }

        return new DispatchTable(maps);
    }

    /// <summary>
    /// Finds the invoker for the exact runtime type of a request.
    /// </summary>
    /// <typeparam name="TResponse">The response type requested by the caller.</typeparam>
    /// <param name="requestType">The runtime type of the request.</param>
    /// <returns>The invoker for the request type.</returns>
    /// <exception cref="ArbitratorConfigurationException">No handler is registered for the request type.</exception>
    public RequestInvoker<TResponse> GetInvoker<TResponse>(Type requestType)
    {
        if (_invokersByRequestType.TryGetValue(requestType, out RequestInvoker? invoker))
        {
            // A registered request type implements exactly one IRequest<>, so its response type is TResponse.
            return (RequestInvoker<TResponse>)invoker;
        }

        throw DispatchErrors.MissingHandler(requestType, typeof(TResponse), FindHandledBaseType(requestType));
    }

    /// <summary>
    /// Finds the invoker for the exact runtime type of a notification.
    /// </summary>
    /// <param name="notificationType">The runtime type of the notification.</param>
    /// <returns>
    /// The invoker, or <see langword="null"/> when no handler is registered for the notification type.
    /// </returns>
    public NotificationInvoker? FindNotificationInvoker(Type notificationType)
    {
        return _invokersByNotificationType.GetValueOrDefault(notificationType);
    }

    /// <summary>
    /// Rejects handlers and behaviors that the provider cannot resolve because they were registered after it was
    /// built.
    /// </summary>
    /// <param name="maps">The request and notification invokers.</param>
    /// <param name="keyedServices">The provider's keyed-service lookup.</param>
    /// <exception cref="ArbitratorConfigurationException">
    /// A handler or behavior is missing from the provider.
    /// </exception>
    private static void EnsureComponentsAreRegistered(DispatchMaps maps, IServiceProviderIsKeyedService keyedServices)
    {
        IEnumerable<Type> requestComponentTypes =
            maps.RequestInvokers.Values.SelectMany(invoker => invoker.ComponentTypes);
        IEnumerable<Type> notificationHandlerTypes =
            maps.NotificationInvokers.Values.SelectMany(invoker => invoker.ComponentTypes);
        Type[] missingComponentTypes =
        [
            .. FindUnregistered(requestComponentTypes, ArbitratorServiceKey.RequestComponents, keyedServices),
            .. FindUnregistered(notificationHandlerTypes, ArbitratorServiceKey.NotificationHandlers, keyedServices),
        ];

        if (missingComponentTypes.Length > 0)
        {
            throw DispatchErrors.ComponentsMissingFromProvider(missingComponentTypes);
        }
    }

    /// <summary>
    /// Finds the component types that a provider has no registration for under a service key.
    /// </summary>
    /// <param name="componentTypes">The component types.</param>
    /// <param name="serviceKey">The Arbitrator service key for the components' role.</param>
    /// <param name="keyedServices">The provider's keyed-service lookup.</param>
    /// <returns>The distinct component types missing from the provider.</returns>
    private static IEnumerable<Type> FindUnregistered(
        IEnumerable<Type> componentTypes,
        ArbitratorServiceKey serviceKey,
        IServiceProviderIsKeyedService keyedServices)
    {
        return componentTypes
            .Distinct()
            .Where(componentType => !keyedServices.IsKeyedService(componentType, serviceKey));
    }

    /// <summary>
    /// Finds the nearest base class of a request type that has a handler, to explain why it was not used.
    /// </summary>
    /// <param name="requestType">The runtime type of the request.</param>
    /// <returns>The nearest handled base class, or <see langword="null"/> when there is none.</returns>
    private Type? FindHandledBaseType(Type requestType)
    {
        for (Type? baseType = requestType.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (_invokersByRequestType.ContainsKey(baseType))
            {
                return baseType;
            }
        }

        return null;
    }
}
