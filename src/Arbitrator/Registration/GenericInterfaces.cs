using System.Diagnostics.CodeAnalysis;

namespace Arbitrator.Registration;

/// <summary>
/// Finds the closed forms of a generic interface that a type implements.
/// </summary>
internal static class GenericInterfaces
{
    /// <summary>
    /// Finds every interface a type implements that is constructed from a generic interface definition.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="genericInterfaceDefinition">
    /// The generic interface definition, such as <c>IRequest&lt;&gt;</c>.
    /// </param>
    /// <returns>The implemented interfaces constructed from the definition.</returns>
    public static Type[] FindImplemented(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type,
        Type genericInterfaceDefinition)
    {
        return
        [
            .. type.GetInterfaces().Where(implemented =>
                implemented.IsGenericType && implemented.GetGenericTypeDefinition() == genericInterfaceDefinition),
        ];
    }
}
