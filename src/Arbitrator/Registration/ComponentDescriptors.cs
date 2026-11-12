using System.Diagnostics.CodeAnalysis;
using Arbitrator.Dispatch;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Registration;

/// <summary>
/// Creates the service descriptors under which handlers and behaviors are resolved.
/// </summary>
internal static class ComponentDescriptors
{
    /// <summary>
    /// Creates a descriptor that registers a request handler as its own implementation type under the request
    /// component key.
    /// </summary>
    /// <param name="handlerType">The request or stream handler type.</param>
    /// <param name="lifetime">The lifetime the handler is registered with.</param>
    /// <returns>The service descriptor.</returns>
    public static ServiceDescriptor ForRequestHandler(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
        ServiceLifetime lifetime)
    {
        return Create(handlerType, ArbitratorServiceKey.RequestComponents, lifetime);
    }

    /// <summary>
    /// Creates a descriptor that registers a notification handler as its own implementation type under the
    /// notification handler key.
    /// </summary>
    /// <param name="handlerType">The notification handler type.</param>
    /// <param name="lifetime">The lifetime the handler is registered with.</param>
    /// <returns>The service descriptor.</returns>
    public static ServiceDescriptor ForNotificationHandler(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
        ServiceLifetime lifetime)
    {
        return Create(handlerType, ArbitratorServiceKey.NotificationHandlers, lifetime);
    }

    /// <summary>
    /// Creates the descriptors for a closed behavior, with its registration's lifetime: one under the request
    /// component key when it implements request or stream behavior contracts, and one under the notification behavior
    /// key when it implements notification behavior contracts.
    /// </summary>
    /// <param name="behaviorType">The closed behavior type.</param>
    /// <param name="registration">The behavior's registration.</param>
    /// <returns>The service descriptors.</returns>
    public static IReadOnlyList<ServiceDescriptor> ForClosedBehavior(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type behaviorType,
        ClosedBehaviorRegistration registration)
    {
        List<ServiceDescriptor> descriptors = [];

        if (registration.Contracts.Count > 0)
        {
            descriptors.Add(Create(behaviorType, ArbitratorServiceKey.RequestComponents, registration.Lifetime));
        }

        if (registration.NotificationTypes.Count > 0)
        {
            descriptors.Add(Create(behaviorType, ArbitratorServiceKey.NotificationBehaviors, registration.Lifetime));
        }

        return descriptors;
    }

    /// <summary>
    /// Creates the descriptor for a behavior placed into a plan, under the plan's behavior key and with the behavior
    /// registration's lifetime.
    /// </summary>
    /// <param name="placement">The placement.</param>
    /// <returns>The service descriptor.</returns>
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public static ServiceDescriptor ForPlacement(BehaviorPlacement placement)
    {
        PlannedBehavior behavior = placement.Behavior;
        return Create(behavior.ClosedType, placement.Plan.BehaviorServiceKey, behavior.Registration.Lifetime);
    }

    /// <summary>
    /// Creates the descriptors for the closed types that open behaviors contribute to a new plan, each with its
    /// behavior registration's lifetime.
    /// </summary>
    /// <param name="plan">The new plan.</param>
    /// <returns>The service descriptors.</returns>
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public static IEnumerable<ServiceDescriptor> ForOpenBehaviors(IBehaviorPlan plan)
    {
        return plan.Behaviors
            .Where(behavior => behavior.Registration is OpenRegistration)
            .Select(behavior => Create(behavior.ClosedType, plan.BehaviorServiceKey, behavior.Registration.Lifetime));
    }

    /// <summary>
    /// Creates a descriptor that registers a component as its own implementation type under a key.
    /// </summary>
    /// <param name="componentType">The component type.</param>
    /// <param name="serviceKey">The Arbitrator service key for the component's role.</param>
    /// <param name="lifetime">The lifetime the component is registered with.</param>
    /// <returns>The service descriptor.</returns>
    private static ServiceDescriptor Create(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type componentType,
        ArbitratorServiceKey serviceKey,
        ServiceLifetime lifetime)
    {
        return new ServiceDescriptor(componentType, serviceKey, componentType, lifetime);
    }
}
