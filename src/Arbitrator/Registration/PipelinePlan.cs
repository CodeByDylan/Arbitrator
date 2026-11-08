using System.Diagnostics.CodeAnalysis;
using Arbitrator.Dispatch;

namespace Arbitrator.Registration;

/// <summary>
/// The handler and ordered behaviors planned for one request type while registrations are still being added.
/// </summary>
internal sealed class PipelinePlan : IBehaviorPlan
{
    private readonly PipelineInvoker _invokerTemplate;
    private readonly PlannedBehaviors _behaviors;

    /// <summary>
    /// Creates a plan with no behaviors for a handler contract.
    /// </summary>
    /// <param name="contract">The handler contract.</param>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    public PipelinePlan(HandlerContract contract)
    {
        Contract = contract;
        _behaviors = new PlannedBehaviors();
        _invokerTemplate = PipelineInvoker.CreateTemplate(
            contract.Kind,
            contract.RequestType,
            contract.ResponseType,
            contract.HandlerType);
    }

    /// <summary>
    /// Creates an independent copy of another plan.
    /// </summary>
    /// <param name="source">The plan to copy.</param>
    private PipelinePlan(PipelinePlan source)
    {
        Contract = source.Contract;
        _invokerTemplate = source._invokerTemplate;
        _behaviors = source._behaviors.Copy();
    }

    /// <summary>
    /// Gets the handler contract of the pipeline.
    /// </summary>
    public HandlerContract Contract { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PlannedBehavior> Behaviors => _behaviors.Items;

    /// <inheritdoc/>
    public ArbitratorServiceKey BehaviorServiceKey => ArbitratorServiceKey.RequestComponents;

    /// <inheritdoc/>
    public void EnsureCanAppend(PlannedBehavior candidate)
    {
        if (_behaviors.FindSameClosedType(candidate) is { } existing)
        {
            throw BehaviorRegistrationErrors.BehaviorWouldRunTwice(new BehaviorConflict(
                candidate.ClosedType,
                Contract.RequestType,
                existing.Registration,
                candidate.Registration));
        }
    }

    /// <inheritdoc/>
    public void Append(PlannedBehavior behavior)
    {
        EnsureCanAppend(behavior);
        _behaviors.Add(behavior);
    }

    /// <inheritdoc/>
    public bool Contains(BehaviorRegistration registration) => _behaviors.Contains(registration);

    /// <summary>
    /// Creates an independent copy of the plan, whose behaviors can change without affecting this plan.
    /// </summary>
    /// <returns>The copy.</returns>
    public PipelinePlan Copy() => new(this);

    /// <summary>
    /// Creates the invoker that runs the planned pipeline.
    /// </summary>
    /// <returns>The invoker.</returns>
    public PipelineInvoker CreateInvoker() => _invokerTemplate.WithBehaviors(_behaviors.ClosedTypes());

    /// <summary>
    /// Describes the planned handler contract and pipeline.
    /// </summary>
    /// <returns>The description.</returns>
    public RequestRegistration Describe() => new(Contract, _behaviors.ClosedTypes());
}
