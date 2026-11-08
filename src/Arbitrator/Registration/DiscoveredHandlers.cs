using System.Collections.Immutable;

namespace Arbitrator.Registration;

/// <summary>
/// The handler types an assembly scan registers, each list in registration order.
/// </summary>
/// <param name="RequestHandlerTypes">The types to register as request handlers.</param>
/// <param name="NotificationHandlerTypes">The types to register as notification handlers.</param>
internal sealed record DiscoveredHandlers(
    ImmutableArray<Type> RequestHandlerTypes,
    ImmutableArray<Type> NotificationHandlerTypes);
