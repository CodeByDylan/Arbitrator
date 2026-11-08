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

    /// <summary>
    /// Explains why assembly scanning may be incompatible with trimming.
    /// </summary>
    public const string ScanningMessage =
        "Arbitrator discovers handler types in an assembly at runtime and inspects the interfaces of request types, "
        + "which trimming cannot analyze; handler types that nothing else references can be removed.";
}
