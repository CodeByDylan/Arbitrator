using System.Collections.Frozen;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Dispatch;

/// <summary>
/// The immutable map from request types to the invokers that run their pipelines.
/// </summary>
internal sealed class DispatchTable
{
    private readonly FrozenDictionary<Type, RequestInvoker> _invokersByRequestType;

    /// <summary>
    /// Creates a dispatch table.
    /// </summary>
    /// <param name="invokersByRequestType">The invoker for each registered request type.</param>
    private DispatchTable(FrozenDictionary<Type, RequestInvoker> invokersByRequestType)
    {
        _invokersByRequestType = invokersByRequestType;
    }

    /// <summary>
    /// Creates the dispatch table of one service provider after verifying that the provider can resolve every
    /// handler and behavior the table refers to.
    /// </summary>
    /// <param name="invokersByRequestType">The invoker for each registered request type.</param>
    /// <param name="keyedServices">
    /// The provider's keyed-service lookup, or <see langword="null"/> when the provider does not offer one.
    /// </param>
    /// <returns>The dispatch table.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// A handler or behavior was registered after the provider was built.
    /// </exception>
    public static DispatchTable Create(
        FrozenDictionary<Type, RequestInvoker> invokersByRequestType,
        IServiceProviderIsKeyedService? keyedServices)
    {
        if (keyedServices is not null)
        {
            EnsureComponentsAreRegistered(invokersByRequestType.Values, keyedServices);
        }

        return new DispatchTable(invokersByRequestType);
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
    /// Rejects handlers and behaviors that the provider cannot resolve because they were registered after it was
    /// built.
    /// </summary>
    /// <param name="invokers">The invokers of the table.</param>
    /// <param name="keyedServices">The provider's keyed-service lookup.</param>
    /// <exception cref="ArbitratorConfigurationException">
    /// A handler or behavior is missing from the provider.
    /// </exception>
    private static void EnsureComponentsAreRegistered(
        IEnumerable<RequestInvoker> invokers,
        IServiceProviderIsKeyedService keyedServices)
    {
        Type[] missingComponentTypes =
        [
            .. invokers
                .SelectMany(invoker => invoker.ComponentTypes)
                .Distinct()
                .Where(componentType => !keyedServices.IsKeyedService(componentType, ArbitratorServiceKey.Instance)),
        ];

        if (missingComponentTypes.Length > 0)
        {
            throw DispatchErrors.ComponentsMissingFromProvider(missingComponentTypes);
        }
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
