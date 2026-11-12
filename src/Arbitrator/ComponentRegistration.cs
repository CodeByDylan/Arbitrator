using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator;

/// <summary>
/// Describes one registered handler or behavior: its implementation type and the lifetime it is registered with.
/// </summary>
public sealed class ComponentRegistration
{
    /// <summary>
    /// Creates the description of a registered component.
    /// </summary>
    /// <param name="implementationType">The implementation type that dispatch resolves.</param>
    /// <param name="lifetime">The lifetime the component is registered with.</param>
    internal ComponentRegistration(Type implementationType, ServiceLifetime lifetime)
    {
        ImplementationType = implementationType;
        Lifetime = lifetime;
    }

    /// <summary>
    /// Gets the implementation type that dispatch resolves. An open behavior appears as the instantiation that runs.
    /// </summary>
    public Type ImplementationType { get; }

    /// <summary>
    /// Gets the lifetime the component is registered with.
    /// </summary>
    public ServiceLifetime Lifetime { get; }
}
