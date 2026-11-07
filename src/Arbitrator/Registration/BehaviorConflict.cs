namespace Arbitrator.Registration;

/// <summary>
/// Two behavior registrations that would place the same closed behavior type twice in one pipeline.
/// </summary>
/// <param name="ClosedBehaviorType">The closed behavior type that would run twice.</param>
/// <param name="RequestType">The request type whose pipeline contains the conflict.</param>
/// <param name="Existing">The registration already in the pipeline.</param>
/// <param name="Rejected">The registration being added.</param>
internal sealed record BehaviorConflict(
    Type ClosedBehaviorType,
    Type RequestType,
    BehaviorRegistration Existing,
    BehaviorRegistration Rejected);
