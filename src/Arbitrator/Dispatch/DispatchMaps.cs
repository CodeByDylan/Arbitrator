using System.Collections.Frozen;

namespace Arbitrator.Dispatch;

/// <summary>
/// The frozen invokers a dispatch table is built from, and their public description.
/// </summary>
/// <param name="PipelineInvokers">The invoker for each registered request and stream request type.</param>
/// <param name="NotificationInvokers">The invoker for each notification type that has handlers.</param>
/// <param name="Registrations">The description of the same registrations.</param>
internal sealed record DispatchMaps(
    FrozenDictionary<Type, PipelineInvoker> PipelineInvokers,
    FrozenDictionary<Type, NotificationInvoker> NotificationInvokers,
    ArbitratorRegistrations Registrations);
