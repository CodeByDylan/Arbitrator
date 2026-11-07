namespace Arbitrator.Registration;

/// <summary>
/// A behavior registered as a closed type, which applies to each behavior contract it implements.
/// </summary>
/// <param name="BehaviorType">The closed behavior type.</param>
/// <param name="Position">The one-based registration position.</param>
/// <param name="Contracts">The behavior contracts the type implements.</param>
internal sealed record ClosedBehaviorRegistration(
    Type BehaviorType,
    int Position,
    IReadOnlyList<RequestResponseTypes> Contracts)
    : BehaviorRegistration(BehaviorType, Position)
{
    /// <summary>
    /// Determines whether the behavior implements the behavior contract for a request and response type.
    /// </summary>
    /// <param name="types">The request and response types.</param>
    /// <returns><see langword="true"/> when the behavior applies to the request type.</returns>
    public bool AppliesTo(RequestResponseTypes types) => Contracts.Contains(types);
}
