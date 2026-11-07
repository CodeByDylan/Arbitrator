using System.Collections.Frozen;

namespace Arbitrator.Dispatch;

/// <summary>
/// The frozen invokers a dispatch table is built from.
/// </summary>
/// <param name="RequestInvokers">The invoker for each registered request type.</param>
/// <param name="NotificationInvokers">The invoker for each notification type that has handlers.</param>
internal sealed record DispatchMaps(
    FrozenDictionary<Type, RequestInvoker> RequestInvokers,
    FrozenDictionary<Type, NotificationInvoker> NotificationInvokers);
