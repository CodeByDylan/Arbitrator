using Arbitrator.Dispatch;

namespace Arbitrator.Registration;

/// <summary>
/// A plan that behaviors are placed into, in registration order, while registrations are still being added.
/// </summary>
internal interface IBehaviorPlan
{
    /// <summary>
    /// Gets the planned behaviors, outermost first.
    /// </summary>
    IReadOnlyList<PlannedBehavior> Behaviors { get; }

    /// <summary>
    /// Gets the private service key under which the plan's behaviors are registered.
    /// </summary>
    ArbitratorServiceKey BehaviorServiceKey { get; }

    /// <summary>
    /// Rejects a behavior whose closed type the plan already contains.
    /// </summary>
    /// <param name="candidate">The behavior to append.</param>
    /// <exception cref="ArbitratorConfigurationException">The closed behavior type would run twice.</exception>
    void EnsureCanAppend(PlannedBehavior candidate);

    /// <summary>
    /// Appends a behavior as the innermost behavior so far.
    /// </summary>
    /// <param name="behavior">The behavior to append.</param>
    /// <exception cref="ArbitratorConfigurationException">The closed behavior type would run twice.</exception>
    void Append(PlannedBehavior behavior);

    /// <summary>
    /// Determines whether a behavior registration contributes to the plan.
    /// </summary>
    /// <param name="registration">The behavior registration.</param>
    /// <returns><see langword="true"/> when the registration is part of the plan.</returns>
    bool Contains(BehaviorRegistration registration);
}
