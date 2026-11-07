namespace Arbitrator.Tests.Examples;

/// <summary>
/// Announces that a customer was deactivated.
/// </summary>
/// <param name="CustomerId">The identifier of the deactivated customer.</param>
/// <param name="Reason">Why the customer was deactivated.</param>
public sealed record CustomerDeactivated(Guid CustomerId, string Reason) : INotification;
