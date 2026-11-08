namespace Arbitrator.Registration;

/// <summary>
/// A behavior registered as a closed type, which applies to each behavior contract it implements.
/// </summary>
/// <param name="BehaviorType">The closed behavior type.</param>
/// <param name="Position">The one-based registration position.</param>
/// <param name="Contracts">The request and stream behavior contracts the type implements.</param>
/// <param name="NotificationTypes">The notification types the type implements a notification behavior for.</param>
internal sealed record ClosedBehaviorRegistration(
    Type BehaviorType,
    int Position,
    IReadOnlyList<RequestResponseTypes> Contracts,
    IReadOnlyList<Type> NotificationTypes)
    : BehaviorRegistration(BehaviorType, Position)
{
    /// <summary>
    /// Determines whether the behavior applies to a request or stream pipeline.
    /// </summary>
    /// <param name="types">The type arguments and kind of the pipeline's handler contract.</param>
    /// <returns><see langword="true"/> when the behavior implements exactly that behavior contract.</returns>
    public bool AppliesTo(RequestResponseTypes types) => Contracts.Contains(types);

    /// <summary>
    /// Determines whether the behavior wraps the handlers of a notification type.
    /// </summary>
    /// <param name="notificationType">The notification type.</param>
    /// <returns><see langword="true"/> when the behavior implements the notification behavior for that type.</returns>
    public bool AppliesToNotification(Type notificationType) => NotificationTypes.Contains(notificationType);
}
