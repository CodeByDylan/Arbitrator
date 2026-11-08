using System.Diagnostics.CodeAnalysis;

namespace Arbitrator.Registration;

/// <summary>
/// A notification behavior registered as an open generic type definition, which wraps the handlers of every
/// notification type that satisfies its generic constraints.
/// </summary>
/// <param name="BehaviorType">The generic type definition, with one type parameter.</param>
/// <param name="Position">The one-based registration position.</param>
internal sealed record OpenNotificationBehaviorRegistration(Type BehaviorType, int Position)
    : OpenRegistration(BehaviorType, Position)
{
    /// <summary>
    /// Closes the behavior over a notification type when the runtime accepts the behavior's generic constraints.
    /// </summary>
    /// <param name="notificationType">The notification type.</param>
    /// <returns>The closed behavior type, or <see langword="null"/> when a constraint is not satisfied.</returns>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public Type? CloseOver(Type notificationType)
    {
        try
        {
            return BehaviorType.MakeGenericType(notificationType);
        }
        catch (ArgumentException exception) when (exception.InnerException is TypeLoadException)
        {
            return null;
        }
    }
}
