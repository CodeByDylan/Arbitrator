namespace Arbitrator.Tests.Support;

/// <summary>
/// Reveals the compile-time type of an expression, so tests can verify overload resolution and type inference.
/// </summary>
public static class StaticType
{
    /// <summary>
    /// Returns the compile-time result type of an expression without evaluating it.
    /// </summary>
    /// <typeparam name="T">The compile-time result type, inferred from <paramref name="expression"/>.</typeparam>
    /// <param name="expression">The expression, which is never invoked.</param>
    /// <returns>The compile-time result type.</returns>
    public static Type OfResult<T>(Func<T> expression) => typeof(T);
}
