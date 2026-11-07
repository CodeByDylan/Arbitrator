namespace Arbitrator.Registration;

/// <summary>
/// A behavior registered with Arbitrator and its place in the global registration order.
/// </summary>
/// <param name="BehaviorType">The registered behavior type.</param>
/// <param name="Position">The one-based registration position. Lower positions run further outside.</param>
internal closed record BehaviorRegistration(Type BehaviorType, int Position);
