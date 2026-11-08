using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Dispatch;

/// <summary>
/// Resolves the handlers and behaviors of a pipeline from a scope, under Arbitrator's private service keys.
/// </summary>
internal static class ComponentResolver
{
    /// <summary>
    /// Resolves behaviors in the given order.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior contract the resolved types implement.</typeparam>
    /// <param name="services">The service provider of the scope.</param>
    /// <param name="behaviorTypes">The closed behavior types, outermost first.</param>
    /// <param name="serviceKey">The key the behaviors are registered under.</param>
    /// <returns>The behavior instances, outermost first.</returns>
    public static TBehavior[] ResolveBehaviors<TBehavior>(
        IServiceProvider services,
        ImmutableArray<Type> behaviorTypes,
        ArbitratorServiceKey serviceKey)
    {
        var behaviors = new TBehavior[behaviorTypes.Length];

        for (int index = 0; index < behaviors.Length; index++)
        {
            behaviors[index] = Resolve<TBehavior>(services, behaviorTypes[index], serviceKey);
        }

        return behaviors;
    }

    /// <summary>
    /// Resolves one handler or behavior.
    /// </summary>
    /// <typeparam name="TComponent">The contract the resolved type implements.</typeparam>
    /// <param name="services">The service provider of the scope.</param>
    /// <param name="componentType">The implementation type to resolve.</param>
    /// <param name="serviceKey">The key the component is registered under.</param>
    /// <returns>The component instance.</returns>
    public static TComponent Resolve<TComponent>(
        IServiceProvider services,
        Type componentType,
        ArbitratorServiceKey serviceKey)
    {
        return (TComponent)services.GetRequiredKeyedService(componentType, serviceKey);
    }
}
