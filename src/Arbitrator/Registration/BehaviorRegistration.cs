using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Registration;

/// <summary>
/// A behavior registered with Arbitrator, its place in the global registration order, and its lifetime.
/// </summary>
/// <param name="BehaviorType">The registered behavior type.</param>
/// <param name="Position">The one-based registration position. Lower positions run further outside.</param>
internal closed record BehaviorRegistration(Type BehaviorType, int Position)
{
    /// <summary>
    /// Gets the lifetime of every service descriptor the behavior is registered with.
    /// </summary>
    public required ServiceLifetime Lifetime { get; init; }
}
