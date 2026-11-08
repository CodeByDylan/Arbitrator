using Arbitrator.Dispatch;

namespace Arbitrator.Registration;

/// <summary>
/// A closed request, handler, or behavior contract that a type implements, and the kind of pipeline it belongs to.
/// </summary>
/// <param name="Contract">The closed contract interface.</param>
/// <param name="Kind">The kind of pipeline.</param>
internal readonly record struct PipelineContract(Type Contract, PipelineKind Kind)
{
    /// <summary>
    /// Gets the type arguments of a two-parameter handler or behavior contract.
    /// </summary>
    public RequestResponseTypes Types
    {
        get
        {
            Type[] typeArguments = Contract.GetGenericArguments();
            return new RequestResponseTypes(typeArguments[0], typeArguments[1], Kind);
        }
    }
}
