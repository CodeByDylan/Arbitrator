using System.Collections.Immutable;

namespace Arbitrator;

/// <summary>
/// Describes the request and notification types that one service provider dispatches, and the handlers and behaviors
/// it runs for them.
/// </summary>
/// <remarks>
/// Resolve it as a singleton from the service provider, for example to test that every request type of an application
/// has a handler. Resolving it freezes the registrations and fails exactly as the first resolution of
/// <see cref="ISender"/> or <see cref="IPublisher"/> does. The description is immutable, and reading it constructs
/// no handler or behavior.
/// </remarks>
public sealed class ArbitratorRegistrations
{
    /// <summary>
    /// Creates a description, sorting both lists by the ordinal order of their types' full names.
    /// </summary>
    /// <param name="requests">The request registrations, in any order.</param>
    /// <param name="notifications">The notification registrations, in any order.</param>
    internal ArbitratorRegistrations(
        IEnumerable<RequestRegistration> requests,
        IEnumerable<NotificationRegistration> notifications)
    {
        Requests = requests.OrderBy(request => request.RequestType.FullName, StringComparer.Ordinal).ToImmutableArray();
        Notifications = notifications
            .OrderBy(notification => notification.NotificationType.FullName, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>
    /// Gets one entry for each request type that has a handler, including stream request types, sorted by the ordinal
    /// order of the request types' full names.
    /// </summary>
    public IReadOnlyList<RequestRegistration> Requests { get; }

    /// <summary>
    /// Gets one entry for each notification type that has at least one handler, sorted by the ordinal order of the
    /// notification types' full names.
    /// </summary>
    public IReadOnlyList<NotificationRegistration> Notifications { get; }
}
