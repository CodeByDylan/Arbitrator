using System.Diagnostics.CodeAnalysis;

namespace Arbitrator.Registration;

/// <summary>
/// A behavior registered as an open generic type definition, which applies to every request whose types satisfy its
/// generic constraints.
/// </summary>
/// <param name="BehaviorType">The generic type definition.</param>
/// <param name="Position">The one-based registration position.</param>
internal sealed record OpenBehaviorRegistration(Type BehaviorType, int Position)
    : BehaviorRegistration(BehaviorType, Position)
{
    /// <summary>
    /// Closes the behavior over a request and response type when the runtime accepts its generic constraints.
    /// </summary>
    /// <param name="types">The request and response types.</param>
    /// <returns>The closed behavior type, or <see langword="null"/> when a constraint is not satisfied.</returns>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public Type? CloseOver(RequestResponseTypes types)
    {
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
