using System.Collections.Frozen;
using System.Text;

namespace Arbitrator.Diagnostics;

/// <summary>
/// Formats types as namespace-qualified C# names for diagnostic messages.
/// </summary>
internal static class TypeNameFormatter
{
    private const char ArityMarker = '`';

    private static readonly FrozenDictionary<Type, string> KeywordsByType = new Dictionary<Type, string>
    {
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(char)] = "char",
        [typeof(decimal)] = "decimal",
        [typeof(double)] = "double",
        [typeof(float)] = "float",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(nint)] = "nint",
        [typeof(nuint)] = "nuint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(object)] = "object",
        [typeof(string)] = "string",
    }.ToFrozenDictionary();

    /// <summary>
    /// Formats a type the way it is written in C#, such as <c>Contoso.Page&lt;System.Guid&gt;</c>,
    /// <c>Contoso.Outer&lt;int&gt;.Inner</c>, <c>Contoso.LoggingBehavior&lt;,&gt;</c>, <c>int?</c>, or
    /// <c>int[][,]</c>.
    /// </summary>
    /// <param name="type">The type to format.</param>
    /// <returns>The formatted type name.</returns>
    public static string Format(Type type)
    {
        if (type.IsArray)
        {
            return FormatArray(type);
        }

        if (Nullable.GetUnderlyingType(type) is { } underlyingType)
        {
            return $"{Format(underlyingType)}?";
        }

        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        return KeywordsByType.GetValueOrDefault(type) ?? FormatNamedType(type);
    }

    /// <summary>
    /// Formats an array type with its rank specifiers in C# order, outermost array first.
    /// </summary>
    /// <param name="arrayType">The array type.</param>
    /// <returns>The formatted type name.</returns>
    private static string FormatArray(Type arrayType)
    {
        var rankSpecifiers = new StringBuilder();
        Type current = arrayType;

        while (current.IsArray && current.GetElementType() is { } elementType)
        {
            rankSpecifiers.Append('[').Append(',', current.GetArrayRank() - 1).Append(']');
            current = elementType;
        }

        return $"{Format(current)}{rankSpecifiers}";
    }

    /// <summary>
    /// Formats a class, struct, interface, or delegate type with its namespace, its enclosing types, and the generic
    /// arguments that belong to each of them.
    /// </summary>
    /// <param name="type">The type to format.</param>
    /// <returns>The formatted type name.</returns>
    private static string FormatNamedType(Type type)
    {
        Type[] typeArguments = type.GetGenericArguments();
        List<string> segments = [];
        int consumedArgumentCount = 0;

        foreach (Type level in EnclosingTypesAndSelf(type))
        {
            int levelArgumentCount = level == type ? typeArguments.Length : level.GetGenericArguments().Length;
            Type[] ownArguments = typeArguments[consumedArgumentCount..levelArgumentCount];
            segments.Add(FormatSegment(level.Name, ownArguments, type.IsGenericTypeDefinition));
            consumedArgumentCount = levelArgumentCount;
        }

        string name = string.Join('.', segments);
        return string.IsNullOrEmpty(type.Namespace) ? name : $"{type.Namespace}.{name}";
    }

    /// <summary>
    /// Lists a type and the types that enclose it, outermost first.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The enclosing types followed by the type itself.</returns>
    private static List<Type> EnclosingTypesAndSelf(Type type)
    {
        List<Type> levels = [];

        for (Type? current = type; current is not null; current = current.DeclaringType)
        {
            levels.Insert(0, current);
        }

        return levels;
    }

    /// <summary>
    /// Formats one segment of a nested type name with the generic arguments it declares.
    /// </summary>
    /// <param name="runtimeName">The runtime name of the segment, such as <c>LoggingBehavior`2</c>.</param>
    /// <param name="ownArguments">The generic arguments the segment declares.</param>
    /// <param name="isDefinition">Whether the arguments are unbound type parameters.</param>
    /// <returns>The formatted segment.</returns>
    private static string FormatSegment(string runtimeName, Type[] ownArguments, bool isDefinition)
    {
        string name = RemoveArity(runtimeName);

        if (ownArguments.Length == 0)
        {
            return name;
        }

        string arguments = isDefinition
            ? new string(',', ownArguments.Length - 1)
            : string.Join(", ", ownArguments.Select(Format));

        return $"{name}<{arguments}>";
    }

    /// <summary>
    /// Removes the generic arity suffix that the runtime appends to generic type names.
    /// </summary>
    /// <param name="name">A runtime type name, such as <c>LoggingBehavior`2</c>.</param>
    /// <returns>The name without its arity suffix.</returns>
    private static string RemoveArity(string name)
    {
        int arityIndex = name.IndexOf(ArityMarker, StringComparison.Ordinal);
        return arityIndex < 0 ? name : name[..arityIndex];
    }
}
