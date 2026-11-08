using System.Diagnostics.CodeAnalysis;
using Arbitrator.Dispatch;

namespace Arbitrator.Registration;

/// <summary>
/// The handler and ordered behaviors planned for one request type while registrations are still being added.
/// </summary>
internal sealed class PipelinePlan
{
    private readonly PipelineInvoker _invokerTemplate;
    private readonly List<PlannedBehavior> _behaviors;

    /// <summary>
    /// Creates a plan with no behaviors for a handler contract.
    /// </summary>
    /// <param name="contract">The handler contract.</param>
    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    public PipelinePlan(HandlerContract contract)
    {
        Contract = contract;
        _behaviors = [];
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
        _behaviors = [.. source._behaviors];
    }

    /// <summary>
    /// Gets the handler contract of the pipeline.
    /// </summary>
    public HandlerContract Contract { get; }

    /// <summary>
    /// Gets the planned behaviors, outermost first.
    /// </summary>
    public IReadOnlyList<PlannedBehavior> Behaviors => _behaviors;

    /// <summary>
    /// Verifies that a behavior can be added without its closed type running twice in the pipeline.
    /// </summary>
    /// <param name="candidate">The behavior to add.</param>
    /// <exception cref="ArbitratorConfigurationException">The closed type is already in the pipeline.</exception>
    public void EnsureCanAppend(PlannedBehavior candidate)
    {
        PlannedBehavior? existing = _behaviors.Find(behavior => behavior.ClosedType == candidate.ClosedType);

        if (existing is not null)
        {
            throw RegistrationErrors.BehaviorWouldRunTwice(new BehaviorConflict(
                candidate.ClosedType,
                Contract.RequestType,
                existing.Registration,
                candidate.Registration));
        }
    }

    /// <summary>
    /// Adds a behavior as the innermost behavior of the pipeline.
    /// </summary>
    /// <param name="behavior">The behavior to add.</param>
    /// <exception cref="ArbitratorConfigurationException">The closed type is already in the pipeline.</exception>
    public void Append(PlannedBehavior behavior)
    {
        EnsureCanAppend(behavior);
        _behaviors.Add(behavior);
    }

    /// <summary>
    /// Determines whether a registration contributes a behavior to the pipeline.
    /// </summary>
    /// <param name="registration">The behavior registration.</param>
    /// <returns><see langword="true"/> when the registration is part of the pipeline.</returns>
    public bool Contains(BehaviorRegistration registration)
    {
        return _behaviors.Exists(behavior => behavior.Registration == registration);
    }

    /// <summary>
    /// Creates an independent copy of the plan, whose behaviors can change without affecting this plan.
    /// </summary>
    /// <returns>The copy.</returns>
    public PipelinePlan Copy() => new(this);

    /// <summary>
    /// Creates the invoker that runs the planned pipeline.
    /// </summary>
    /// <returns>The invoker.</returns>
    public PipelineInvoker CreateInvoker()
    {
        return _invokerTemplate.WithBehaviors([.. _behaviors.Select(behavior => behavior.ClosedType)]);
    }
}
