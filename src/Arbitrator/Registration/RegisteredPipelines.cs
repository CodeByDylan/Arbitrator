using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Arbitrator.Dispatch;

namespace Arbitrator.Registration;

/// <summary>
/// The planned pipeline of every request type registered with one service collection.
/// </summary>
internal sealed class RegisteredPipelines
{
    private readonly Dictionary<Type, PipelinePlan> _plansByRequestType = [];

    /// <summary>
    /// Rejects a handler type that is already registered.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <exception cref="ArbitratorConfigurationException">The handler type is already registered.</exception>
    public void EnsureHandlerTypeIsNew(Type handlerType)
    {
        if (_plansByRequestType.Values.Any(plan => plan.Contract.HandlerType == handlerType))
        {
            throw RegistrationErrors.DuplicateHandlerType(handlerType);
        }
    }

    /// <summary>
    /// Rejects handler contracts for request types that already have a handler.
    /// </summary>
    /// <param name="contracts">The new handler contracts.</param>
    /// <exception cref="ArbitratorConfigurationException">A request type already has a handler.</exception>
    public void EnsureRequestTypesHaveNoHandler(IReadOnlyList<HandlerContract> contracts)
    {
        foreach (HandlerContract contract in contracts)
        {
            if (_plansByRequestType.TryGetValue(contract.RequestType, out PipelinePlan? existing))
            {
                throw RegistrationErrors.DuplicateRequestHandler(
                    contract.RequestType,
                    existing.Contract.HandlerType,
                    contract.HandlerType);
            }
        }
    }

    /// <summary>
    /// Adds the pipelines of newly registered request types.
    /// </summary>
    /// <param name="plans">The new pipeline plans.</param>
    public void AddRange(IEnumerable<PipelinePlan> plans)
    {
        foreach (PipelinePlan plan in plans)
        {
            _plansByRequestType.Add(plan.Contract.RequestType, plan);
        }
    }

    /// <summary>
    /// Finds the registered pipelines a closed behavior applies to.
    /// </summary>
    /// <param name="registration">The closed behavior registration.</param>
    /// <returns>The placements of the behavior.</returns>
    public List<BehaviorPlacement> PlaceClosedBehavior(ClosedBehaviorRegistration registration)
    {
        var plannedBehavior = new PlannedBehavior(registration.BehaviorType, registration);

        return
        [
            .. _plansByRequestType.Values
                .Where(plan => registration.AppliesTo(plan.Contract.Types))
                .Select(plan => new BehaviorPlacement(plan, plannedBehavior)),
        ];
    }

    /// <summary>
    /// Finds the registered pipelines an open behavior applies to.
    /// </summary>
    /// <param name="registration">The open behavior registration.</param>
    /// <returns>The placements of the closed behavior types.</returns>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public List<BehaviorPlacement> PlaceOpenBehavior(OpenBehaviorRegistration registration)
    {
        List<BehaviorPlacement> placements = [];

        foreach (PipelinePlan plan in _plansByRequestType.Values)
        {
            if (registration.CloseOver(plan.Contract.Types) is { } closedType)
            {
                placements.Add(new BehaviorPlacement(plan, new PlannedBehavior(closedType, registration)));
            }
        }

        return placements;
    }

    /// <summary>
    /// Determines whether a behavior registration is part of any request pipeline.
    /// </summary>
    /// <param name="registration">The behavior registration.</param>
    /// <returns><see langword="true"/> when some pipeline contains the registration.</returns>
    public bool Contains(BehaviorRegistration registration)
    {
        return _plansByRequestType.Values.Any(plan => plan.Contains(registration));
    }

    /// <summary>
    /// Describes every registered request and stream request type, in no particular order.
    /// </summary>
    /// <returns>One description per request type.</returns>
    public IEnumerable<RequestRegistration> Describe() => _plansByRequestType.Values.Select(plan => plan.Describe());

    /// <summary>
    /// Creates the invoker for every registered request and stream request type.
    /// </summary>
    /// <returns>The invokers, keyed by request type.</returns>
    public FrozenDictionary<Type, PipelineInvoker> CreateInvokers()
    {
        return _plansByRequestType.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.CreateInvoker());
    }

    /// <summary>
    /// Creates an independent copy of the registered pipelines.
    /// </summary>
    /// <returns>The copy.</returns>
    public RegisteredPipelines Copy()
    {
        var copy = new RegisteredPipelines();
        copy.AddRange(_plansByRequestType.Values.Select(plan => plan.Copy()));
        return copy;
    }
}
