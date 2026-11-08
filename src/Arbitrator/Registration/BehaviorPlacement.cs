namespace Arbitrator.Registration;

/// <summary>
/// A behavior that a new registration adds to the end of an existing plan.
/// </summary>
/// <param name="Plan">The plan that receives the behavior.</param>
/// <param name="Behavior">The behavior to add.</param>
internal readonly record struct BehaviorPlacement(IBehaviorPlan Plan, PlannedBehavior Behavior);
