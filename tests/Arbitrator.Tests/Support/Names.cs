namespace Arbitrator.Tests.Support;

/// <summary>
/// Produces the type names that Arbitrator's diagnostic messages contain.
/// </summary>
public static class Names
{
    /// <summary>
    /// Returns the namespace-qualified name of a non-generic type, with nested types separated by dots.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <returns>The name as it appears in diagnostic messages.</returns>
    public static string Of<T>()
    {
        Type type = typeof(T);
        return (type.FullName ?? type.Name).Replace('+', '.');
    }
}
