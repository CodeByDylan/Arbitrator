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
    /// Creates a transient descriptor that registers a handler or behavior as its own implementation type under the
    /// Arbitrator service key.
    /// </summary>
    /// <param name="componentType">The closed handler or behavior type.</param>
    /// <returns>The service descriptor.</returns>
    public static ServiceDescriptor Create(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type componentType)
    {
        return new ServiceDescriptor(
            componentType,
            ArbitratorServiceKey.Instance,
            componentType,
            ServiceLifetime.Transient);
    }
}
