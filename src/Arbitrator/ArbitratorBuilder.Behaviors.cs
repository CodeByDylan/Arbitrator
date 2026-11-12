using System.Diagnostics.CodeAnalysis;
using Arbitrator.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator;

public sealed partial class ArbitratorBuilder
{
    /// <summary>
    /// Registers a transient closed behavior at the next position in the pipeline order. It applies to every behavior
    /// contract it implements.
    /// </summary>
    /// <typeparam name="TBehavior">
    /// A concrete class implementing <see cref="IPipelineBehavior{TRequest, TResponse}"/>,
    /// <see cref="IStreamPipelineBehavior{TRequest, TItem}"/>, or <see cref="INotificationBehavior{TNotification}"/>
    /// one or more times.
    /// </typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="ArbitratorConfigurationException">
    /// The behavior type is abstract, implements no behavior contract, is already registered, or would run twice; or
    /// a service provider has attempted to resolve <see cref="ISender"/> or <see cref="IPublisher"/>; or the service
    /// collection is read-only.
    /// </exception>
    public ArbitratorBuilder AddBehavior<
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] TBehavior>()
        where TBehavior : class
    {
        return AddBehavior<TBehavior>(ServiceLifetime.Transient);
    }

    /// <inheritdoc cref="AddBehavior{TBehavior}()"/>
    /// <summary>
    /// Registers a closed behavior with a lifetime at the next position in the pipeline order. It applies to every
    /// behavior contract it implements, with the same lifetime in every role.
    /// </summary>
    /// <param name="lifetime">
    /// The lifetime of the behavior. A scoped or singleton behavior is shared by every dispatch that resolves it, so it
    /// must not keep request-specific state, and a singleton must not depend on scoped services.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is not a defined value.</exception>
    public ArbitratorBuilder AddBehavior<
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] TBehavior>(ServiceLifetime lifetime)
        where TBehavior : class
    {
        EnsureLifetimeIsDefined(lifetime);
        EnsureServicesAreWritable();
        AddServices(_registry.RegisterClosedBehavior(typeof(TBehavior), lifetime));
        return this;
    }

    /// <summary>
    /// Registers a transient open generic behavior at the next position in the pipeline order. It applies to every
    /// request or notification of its kind whose types satisfy the runtime generic constraints of the behavior.
    /// </summary>
    /// <param name="openBehaviorType">
    /// A generic type definition, such as <c>typeof(LoggingBehavior&lt;,&gt;)</c>, that implements exactly one of
    /// <see cref="IPipelineBehavior{TRequest, TResponse}"/>, <see cref="IStreamPipelineBehavior{TRequest, TItem}"/>,
    /// or <see cref="INotificationBehavior{TNotification}"/> with its type parameters in order.
    /// </param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="openBehaviorType"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArbitratorConfigurationException">
    /// The type is not a suitable generic type definition, is already registered, or would run twice for a request;
    /// or a service provider has attempted to resolve <see cref="ISender"/> or <see cref="IPublisher"/>; or the service
    /// collection is read-only.
    /// </exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public ArbitratorBuilder AddOpenBehavior(
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] Type openBehaviorType)
    {
        return AddOpenBehavior(openBehaviorType, ServiceLifetime.Transient);
    }

    /// <inheritdoc cref="AddOpenBehavior(Type)"/>
    /// <summary>
    /// Registers an open generic behavior with a lifetime at the next position in the pipeline order. Every closed
    /// instantiation of it gets that lifetime.
    /// </summary>
    /// <param name="openBehaviorType">
    /// A generic type definition that implements exactly one behavior contract with its type parameters in order.
    /// </param>
    /// <param name="lifetime">
    /// The lifetime of every closed instantiation. A scoped or singleton behavior is shared by every dispatch that
    /// resolves it, so it must not keep request-specific state, and a singleton must not depend on scoped services.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is not a defined value.</exception>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public ArbitratorBuilder AddOpenBehavior(
        [DynamicallyAccessedMembers(RegistrationReflection.ComponentMembers)] Type openBehaviorType,
        ServiceLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(openBehaviorType);
        EnsureLifetimeIsDefined(lifetime);
        EnsureServicesAreWritable();
        AddServices(_registry.RegisterOpenBehavior(openBehaviorType, lifetime));
        return this;
    }
}
