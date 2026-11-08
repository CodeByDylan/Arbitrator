using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Dispatch;

/// <summary>
/// Resolves the handlers and behaviors of a pipeline from a scope, under Arbitrator's private service key.
/// </summary>
internal static class ComponentResolver
{
    /// <summary>
    /// Resolves behaviors in the given order.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior contract the resolved types implement.</typeparam>
    /// <param name="services">The service provider of the scope.</param>
    /// <param name="behaviorTypes">The closed behavior types, outermost first.</param>
    /// <returns>The behavior instances, outermost first.</returns>
    public static TBehavior[] ResolveBehaviors<TBehavior>(IServiceProvider services, ImmutableArray<Type> behaviorTypes)
    {
        var behaviors = new TBehavior[behaviorTypes.Length];

        for (int index = 0; index < behaviors.Length; index++)
        {
            behaviors[index] = Resolve<TBehavior>(services, behaviorTypes[index]);
        }

        return behaviors;
    }

    /// <summary>
    /// Resolves one handler or behavior.
    /// </summary>
    /// <typeparam name="TComponent">The contract the resolved type implements.</typeparam>
    /// <param name="services">The service provider of the scope.</param>
    /// <param name="componentType">The implementation type to resolve.</param>
    /// <returns>The component instance.</returns>
    public static TComponent Resolve<TComponent>(IServiceProvider services, Type componentType)
    {
        return (TComponent)services.GetRequiredKeyedService(componentType, ArbitratorServiceKey.RequestComponents);
    }
}
