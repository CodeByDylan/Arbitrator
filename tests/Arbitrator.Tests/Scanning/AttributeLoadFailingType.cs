using System.Reflection;

namespace Arbitrator.Tests.Scanning;

/// <summary>
/// A type whose attributes cannot be read, as when an assembly that defines one of them is missing at runtime.
/// </summary>
public sealed class AttributeLoadFailingType : TypeDelegator
{
    /// <summary>
    /// Creates a type that behaves like another type, except that reading its attributes fails.
    /// </summary>
    /// <param name="delegatingType">The type to behave like.</param>
    /// <param name="failure">The exception thrown when the attributes are read.</param>
    public AttributeLoadFailingType(Type delegatingType, FileNotFoundException failure)
        : base(delegatingType)
    {
        Failure = failure;
    }

    /// <summary>
    /// Gets the exception thrown when the attributes are read.
    /// </summary>
    public FileNotFoundException Failure { get; }

    /// <inheritdoc/>
    public override bool IsGenericTypeDefinition => typeImpl.IsGenericTypeDefinition;

    /// <inheritdoc/>
    public override bool IsDefined(Type attributeType, bool inherit) => throw Failure;

    /// <inheritdoc/>
    public override Type[] GetGenericArguments() => typeImpl.GetGenericArguments();
}
