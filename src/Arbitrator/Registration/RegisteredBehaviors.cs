using System.Diagnostics.CodeAnalysis;

namespace Arbitrator.Registration;

/// <summary>
/// The behaviors registered with one service collection, in global registration order.
/// </summary>
internal sealed class RegisteredBehaviors
{
    private readonly List<BehaviorRegistration> _registrations = [];

    /// <summary>
    /// Gets the registration position that the next behavior receives.
    /// </summary>
    public int NextPosition => _registrations.Count + 1;

    /// <summary>
    /// Gets the behaviors registered as closed types.
    /// </summary>
    public IEnumerable<ClosedBehaviorRegistration> ClosedRegistrations =>
        _registrations.OfType<ClosedBehaviorRegistration>();

    /// <summary>
    /// Rejects a behavior type that is already registered.
    /// </summary>
    /// <param name="behaviorType">The behavior type.</param>
    /// <exception cref="ArbitratorConfigurationException">The behavior type is already registered.</exception>
    public void EnsureTypeIsNew(Type behaviorType)
    {
        BehaviorRegistration? existing = _registrations.Find(
            registration => registration.BehaviorType == behaviorType);

        if (existing is not null)
        {
            throw RegistrationErrors.DuplicateBehavior(existing);
        }
    }

    /// <summary>
    /// Records a behavior registration as the last in registration order.
    /// </summary>
    /// <param name="registration">The behavior registration.</param>
    public void Add(BehaviorRegistration registration) => _registrations.Add(registration);

    /// <summary>
    /// Creates an independent copy of the registered behaviors.
    /// </summary>
    /// <returns>The copy.</returns>
    public RegisteredBehaviors Copy()
    {
        var copy = new RegisteredBehaviors();
        copy._registrations.AddRange(_registrations);
        return copy;
    }

    /// <summary>
    /// Finds the closed behavior types that apply to a handler contract, outermost first.
    /// </summary>
    /// <param name="contract">The handler contract.</param>
    /// <returns>The applicable behaviors, in registration order.</returns>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public IEnumerable<PlannedBehavior> PlanFor(HandlerContract contract)
    {
        foreach (BehaviorRegistration registration in _registrations)
        {
            if (CloseOver(registration, contract.Types) is { } closedType)
            {
                yield return new PlannedBehavior(closedType, registration);
            }
        }
    }

    /// <summary>
    /// Finds the closed behavior type a registration contributes to a request's pipeline.
    /// </summary>
    /// <param name="registration">The behavior registration.</param>
    /// <param name="types">The request and response types.</param>
    /// <returns>The closed behavior type, or <see langword="null"/> when the behavior does not apply.</returns>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    private static Type? CloseOver(BehaviorRegistration registration, RequestResponseTypes types) => registration switch
    {
        ClosedBehaviorRegistration closed => closed.AppliesTo(types) ? closed.BehaviorType : null,
        OpenBehaviorRegistration open => open.CloseOver(types),
    };
}
