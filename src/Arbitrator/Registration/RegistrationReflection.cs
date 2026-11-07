using System.Diagnostics.CodeAnalysis;

namespace Arbitrator.Registration;

/// <summary>
/// The trimming and dynamic-code requirements shared by the registration members that use reflection.
/// </summary>
internal static class RegistrationReflection
{
    /// <summary>
    /// The members that must be preserved on handler and behavior types.
    /// </summary>
    public const DynamicallyAccessedMemberTypes ComponentMembers =
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces;

    /// <summary>
    /// Explains why registration requires dynamic code.
    /// </summary>
    public const string DynamicCodeMessage =
        "Arbitrator closes generic types over request and response types at runtime, which Native AOT cannot "
        + "guarantee.";

    /// <summary>
    /// Explains why registration may be incompatible with trimming.
    /// </summary>
    public const string UnreferencedCodeMessage =
        "Arbitrator inspects the interfaces of request types and closes generic behavior types at runtime, which "
        + "trimming cannot analyze.";
}
