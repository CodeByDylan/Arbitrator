namespace Arbitrator.Registration;

/// <summary>
/// A behavior registered as an open generic type definition, of any kind.
/// </summary>
/// <param name="BehaviorType">The generic type definition.</param>
/// <param name="Position">The one-based registration position.</param>
internal closed record OpenRegistration(Type BehaviorType, int Position)
    : BehaviorRegistration(BehaviorType, Position);
