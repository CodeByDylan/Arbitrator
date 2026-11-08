using System.Reflection;
using Arbitrator.Diagnostics;

namespace Arbitrator.Registration;

/// <summary>
/// Creates the exceptions that report assembly scans that cannot register anything.
/// </summary>
internal static class ScanningErrors
{
    private const int MaxLoaderMessages = 5;
    private const string UnnamedAssembly = "(unnamed)";

    /// <summary>
    /// Reports an assembly whose types cannot all be loaded.
    /// </summary>
    /// <param name="assembly">The scanned assembly.</param>
    /// <param name="exception">The exception that reported the types that could not be loaded.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException TypesCouldNotBeLoaded(
        Assembly assembly,
        ReflectionTypeLoadException exception)
    {
        string[] loaderMessages =
        [
            .. exception.LoaderExceptions.OfType<Exception>().Select(loaderException => loaderException.Message)
                .Distinct(StringComparer.Ordinal),
        ];

        return new(
            $"Assembly '{Name(assembly)}' cannot be scanned because some of its types could not be loaded"
                + DescribeLoaderMessages(loaderMessages),
            exception);
    }

    /// <summary>
    /// Reports a handler type whose attributes cannot be read because an assembly they come from cannot be loaded.
    /// </summary>
    /// <param name="assembly">The scanned assembly.</param>
    /// <param name="handlerType">The handler type.</param>
    /// <param name="exception">The exception thrown while reading the attributes.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException AttributesCouldNotBeLoaded(
        Assembly assembly,
        Type handlerType,
        Exception exception)
    {
        return new(
            $"Assembly '{Name(assembly)}' cannot be scanned because the attributes of handler type "
                + $"'{TypeNameFormatter.Format(handlerType)}' could not be loaded: {exception.Message}",
            exception);
    }

    /// <summary>
    /// Reports an assembly that defines no request handler or notification handler types.
    /// </summary>
    /// <param name="assembly">The scanned assembly.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException NoHandlerTypes(Assembly assembly)
    {
        return new($"Assembly '{Name(assembly)}' contains no request handler or notification handler types. "
            + "Check that the marker type or assembly passed to AddHandlersFromAssembly is the one that defines the "
            + "handlers.");
    }

    /// <summary>
    /// Reports a filter that excluded every handler type of an assembly.
    /// </summary>
    /// <param name="assembly">The scanned assembly.</param>
    /// <param name="excludedCount">The number of handler types the filter excluded.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException FilterExcludedEveryHandler(Assembly assembly, int excludedCount)
    {
        return new($"The filter excluded all {excludedCount} handler types found in assembly '{Name(assembly)}', "
            + "so nothing was registered.");
    }

    /// <summary>
    /// Ends the type-load message with the first loader messages and a count of the rest, or with a pointer to the
    /// inner exception when there are none.
    /// </summary>
    /// <param name="loaderMessages">The distinct loader messages, in their original order.</param>
    /// <returns>The end of the message.</returns>
    private static string DescribeLoaderMessages(string[] loaderMessages)
    {
        if (loaderMessages.Length == 0)
        {
            return ". The inner exception has the details.";
        }

        string shown = string.Join(" ", loaderMessages.Take(MaxLoaderMessages));
        int remaining = loaderMessages.Length - MaxLoaderMessages;

        return remaining > 0 ? $": {shown} (and {remaining} more)" : $": {shown}";
    }

    /// <summary>
    /// Formats an assembly for a message by its simple name.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The simple name, or a placeholder when the assembly has none.</returns>
    private static string Name(Assembly assembly) => assembly.GetName().Name ?? UnnamedAssembly;
}
