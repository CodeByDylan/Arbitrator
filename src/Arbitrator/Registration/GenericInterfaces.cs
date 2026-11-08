using System.Diagnostics.CodeAnalysis;
using Arbitrator.Dispatch;

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

    /// <summary>
    /// Finds every closed contract of one role that a type implements, across all pipeline kinds.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="contractOf">Selects the generic definition of the role's contract for a pipeline kind.</param>
    /// <returns>The implemented contracts, grouped by pipeline kind.</returns>
    public static IReadOnlyList<PipelineContract> FindPipelineContracts(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type,
        Func<PipelineKind, Type> contractOf)
    {
        List<PipelineContract> contracts = [];

        foreach (PipelineKind kind in PipelineKind.All)
        {
            Type[] implemented = FindImplemented(type, contractOf(kind));
            contracts.AddRange(implemented.Select(contract => new PipelineContract(contract, kind)));
        }

        return contracts;
    }
}
