namespace Arbitrator.Registration;

/// <summary>
/// Two behavior registrations that would place the same closed behavior type twice around one handler.
/// </summary>
/// <param name="ClosedBehaviorType">The closed behavior type that would run twice.</param>
/// <param name="TargetType">The request or notification type whose plan contains the conflict.</param>
/// <param name="Existing">The registration already in the plan.</param>
/// <param name="Rejected">The registration being added.</param>
internal sealed record BehaviorConflict(
    Type ClosedBehaviorType,
    Type TargetType,
    BehaviorRegistration Existing,
    BehaviorRegistration Rejected);
