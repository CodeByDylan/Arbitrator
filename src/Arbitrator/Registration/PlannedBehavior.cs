namespace Arbitrator.Registration;

/// <summary>
/// A closed behavior type in one request's pipeline and the registration that produced it.
/// </summary>
/// <param name="ClosedType">The closed behavior type resolved at dispatch.</param>
/// <param name="Registration">The registration that produced the closed type.</param>
internal sealed record PlannedBehavior(Type ClosedType, BehaviorRegistration Registration);
