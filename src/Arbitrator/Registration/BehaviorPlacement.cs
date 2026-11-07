namespace Arbitrator.Registration;

/// <summary>
/// A behavior that a new registration adds to the end of an existing request pipeline.
/// </summary>
/// <param name="Plan">The pipeline that receives the behavior.</param>
/// <param name="Behavior">The behavior to add.</param>
internal readonly record struct BehaviorPlacement(PipelinePlan Plan, PlannedBehavior Behavior);
