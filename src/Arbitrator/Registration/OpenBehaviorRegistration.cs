using System.Diagnostics.CodeAnalysis;
using Arbitrator.Dispatch;

namespace Arbitrator.Registration;

/// <summary>
/// A behavior registered as an open generic type definition, which applies to every request of its pipeline kind
/// whose types satisfy its generic constraints.
/// </summary>
/// <param name="BehaviorType">The generic type definition.</param>
/// <param name="Position">The one-based registration position.</param>
/// <param name="Kind">The kind of pipeline the behavior contract belongs to.</param>
internal sealed record OpenBehaviorRegistration(Type BehaviorType, int Position, PipelineKind Kind)
    : OpenRegistration(BehaviorType, Position)
{
    /// <summary>
    /// Closes the behavior over a contract's types when the contract is of the behavior's kind and the runtime
    /// accepts the behavior's generic constraints.
    /// </summary>
    /// <param name="types">The contract's type arguments and kind.</param>
    /// <returns>
    /// The closed behavior type, or <see langword="null"/> when the kind differs or a constraint is not satisfied.
    /// </returns>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public Type? CloseOver(RequestResponseTypes types)
    {
        if (types.Kind != Kind)
        {
            return null;
        }

        try
        {
            return BehaviorType.MakeGenericType(types.RequestType, types.ResponseType);
        }
        catch (ArgumentException exception) when (exception.InnerException is TypeLoadException)
        {
            return null;
        }
    }
}
