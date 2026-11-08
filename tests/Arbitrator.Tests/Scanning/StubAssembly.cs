using System.Reflection;

namespace Arbitrator.Tests.Scanning;

/// <summary>
/// An assembly that reports a chosen simple name and either a chosen set of types or a type-load failure.
/// </summary>
public sealed class StubAssembly : Assembly
{
    private readonly string _simpleName;
    private readonly Func<Type[]> _getTypes;

    /// <summary>
    /// Creates an assembly with a simple name whose types come from a function.
    /// </summary>
    /// <param name="simpleName">The simple name of the assembly.</param>
    /// <param name="getTypes">Returns the types of the assembly, or throws.</param>
    private StubAssembly(string simpleName, Func<Type[]> getTypes)
    {
        _simpleName = simpleName;
        _getTypes = getTypes;
    }

    /// <inheritdoc/>
    public override string FullName => $"{_simpleName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

    /// <summary>
    /// Creates an assembly that defines exactly the given types, in the given order.
    /// </summary>
    /// <param name="simpleName">The simple name of the assembly.</param>
    /// <param name="types">The types of the assembly.</param>
    /// <returns>The assembly.</returns>
    public static StubAssembly Defining(string simpleName, params Type[] types) => new(simpleName, () => types);

    /// <summary>
    /// Creates an assembly whose types cannot be loaded.
    /// </summary>
    /// <param name="simpleName">The simple name of the assembly.</param>
    /// <param name="exception">The exception thrown when the types are read.</param>
    /// <returns>The assembly.</returns>
    public static StubAssembly FailingToLoad(string simpleName, ReflectionTypeLoadException exception)
    {
        return new(simpleName, () => throw exception);
    }

    /// <inheritdoc/>
    public override AssemblyName GetName(bool copiedName) => new(FullName);

    /// <inheritdoc/>
    public override Type[] GetTypes() => _getTypes();
}
