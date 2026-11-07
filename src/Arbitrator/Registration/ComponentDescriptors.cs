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
    /// Creates a transient descriptor that registers a request handler or behavior as its own implementation type
    /// under the request component key.
    /// </summary>
    /// <param name="componentType">The closed request handler or behavior type.</param>
    /// <returns>The service descriptor.</returns>
    public static ServiceDescriptor ForRequestComponent(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type componentType)
    {
        return Create(componentType, ArbitratorServiceKey.RequestComponents);
    }

    /// <summary>
    /// Creates a transient descriptor that registers a notification handler as its own implementation type under the
    /// notification handler key.
    /// </summary>
    /// <param name="handlerType">The notification handler type.</param>
    /// <returns>The service descriptor.</returns>
    public static ServiceDescriptor ForNotificationHandler(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType)
    {
        return Create(handlerType, ArbitratorServiceKey.NotificationHandlers);
    }

    /// <summary>
    /// Creates a transient descriptor that registers a component as its own implementation type under a key.
    /// </summary>
    /// <param name="componentType">The component type.</param>
    /// <param name="serviceKey">The Arbitrator service key for the component's role.</param>
    /// <returns>The service descriptor.</returns>
    private static ServiceDescriptor Create(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type componentType,
        ArbitratorServiceKey serviceKey)
    {
        return new ServiceDescriptor(componentType, serviceKey, componentType, ServiceLifetime.Transient);
    }
}
