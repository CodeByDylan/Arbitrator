using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Arbitrator.Registration;

/// <summary>
/// Finds the request handler and notification handler types that an assembly defines.
/// </summary>
internal static class HandlerAssemblyScanner
{
    /// <summary>
    /// Finds the concrete handler classes of an assembly, sorted by full name, that a filter accepts.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="filter">Decides, for each handler type found, whether to register it.</param>
    /// <returns>The handler types to register.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// Some types of the assembly cannot be loaded, the assembly defines no handler types, or the filter excludes
    /// every handler type.
    /// </exception>
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    public static DiscoveredHandlers Discover(Assembly assembly, Func<Type, bool> filter)
    {
        Type[] candidates = FindCandidates(assembly);

        if (candidates.Length == 0)
        {
            throw ScanningErrors.NoHandlerTypes(assembly);
        }

        Type[] accepted = [.. candidates.Where(filter)];

        if (accepted.Length == 0)
        {
            throw ScanningErrors.FilterExcludedEveryHandler(assembly, candidates.Length);
        }

        return new DiscoveredHandlers(
            [.. accepted.Where(ImplementsRequestHandlerContract)],
            [.. accepted.Where(ImplementsNotificationHandlerContract)]);
    }

    /// <summary>
    /// Finds the types of an assembly that can be registered as handlers, sorted by full name.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The candidate handler types.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// Some types of the assembly, or the attributes of a handler type, cannot be loaded.
    /// </exception>
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    private static Type[] FindCandidates(Assembly assembly)
    {
        return
        [
            .. LoadTypes(assembly)
                .Where(type => IsCandidate(type, assembly))
                .OrderBy(type => type.FullName, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Loads every type an assembly defines, including non-public and nested types.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The types.</returns>
    /// <exception cref="ArbitratorConfigurationException">Some types of the assembly cannot be loaded.</exception>
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    private static Type[] LoadTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            throw ScanningErrors.TypesCouldNotBeLoaded(assembly, exception);
        }
    }

    /// <summary>
    /// Determines whether a type is a concrete, closed, source-defined class that implements a handler contract.
    /// </summary>
    /// <remarks>
    /// Attributes are read only for types that implement a handler contract, because reading them loads the
    /// assemblies that define them.
    /// </remarks>
    /// <param name="type">The type.</param>
    /// <param name="assembly">The scanned assembly.</param>
    /// <returns><see langword="true"/> when the type can be registered as a handler.</returns>
    /// <exception cref="ArbitratorConfigurationException">An attribute of a handler type cannot be loaded.</exception>
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    private static bool IsCandidate(Type type, Assembly assembly)
    {
        bool isConcreteClosedClass = type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false };

        return isConcreteClosedClass
            && (ImplementsRequestHandlerContract(type) || ImplementsNotificationHandlerContract(type))
            && !IsCompilerGenerated(type, assembly);
    }

    /// <summary>
    /// Determines whether the compiler, rather than source code, defined a type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="assembly">The scanned assembly.</param>
    /// <returns><see langword="true"/> when the type is marked as compiler-generated.</returns>
    /// <exception cref="ArbitratorConfigurationException">An attribute of the type cannot be loaded.</exception>
    private static bool IsCompilerGenerated(Type type, Assembly assembly)
    {
        try
        {
            return type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or FileLoadException or TypeLoadException)
        {
            throw ScanningErrors.AttributesCouldNotBeLoaded(assembly, type, exception);
        }
    }

    /// <summary>
    /// Determines whether a type implements a request handler contract.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> when the type implements a request handler contract.</returns>
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    private static bool ImplementsRequestHandlerContract(Type type)
    {
        return GenericInterfaces.FindImplemented(type, typeof(IRequestHandler<,>)).Length > 0;
    }

    /// <summary>
    /// Determines whether a type implements a notification handler contract.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> when the type implements a notification handler contract.</returns>
    [RequiresUnreferencedCode(RegistrationReflection.ScanningMessage)]
    private static bool ImplementsNotificationHandlerContract(Type type)
    {
        return GenericInterfaces.FindImplemented(type, typeof(INotificationHandler<>)).Length > 0;
    }
}
