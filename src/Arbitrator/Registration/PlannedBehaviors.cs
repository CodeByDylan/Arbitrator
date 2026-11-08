using System.Collections.Immutable;

namespace Arbitrator.Registration;

/// <summary>
/// The behaviors planned around one handler contract or notification type, outermost first.
/// </summary>
internal sealed class PlannedBehaviors
{
    private readonly List<PlannedBehavior> _behaviors;

    /// <summary>
    /// Creates an empty list of planned behaviors.
    /// </summary>
    public PlannedBehaviors()
    {
        _behaviors = [];
    }

    /// <summary>
    /// Creates a list holding the given behaviors.
    /// </summary>
    /// <param name="behaviors">The behaviors, outermost first.</param>
    private PlannedBehaviors(IEnumerable<PlannedBehavior> behaviors)
    {
        _behaviors = [.. behaviors];
    }

    /// <summary>
    /// Gets the planned behaviors, outermost first.
    /// </summary>
    public IReadOnlyList<PlannedBehavior> Items => _behaviors;

    /// <summary>
    /// Finds a planned behavior with the same closed type as a candidate.
    /// </summary>
    /// <param name="candidate">The behavior about to be added.</param>
    /// <returns>The planned behavior with the same closed type, or <see langword="null"/> when there is none.</returns>
    public PlannedBehavior? FindSameClosedType(PlannedBehavior candidate)
    {
        return _behaviors.Find(behavior => behavior.ClosedType == candidate.ClosedType);
    }

    /// <summary>
    /// Adds a behavior as the innermost so far.
    /// </summary>
    /// <param name="behavior">The behavior to add.</param>
    public void Add(PlannedBehavior behavior) => _behaviors.Add(behavior);

    /// <summary>
    /// Determines whether a registration contributes one of the planned behaviors.
    /// </summary>
    /// <param name="registration">The behavior registration.</param>
    /// <returns><see langword="true"/> when the registration is among the planned behaviors.</returns>
    public bool Contains(BehaviorRegistration registration)
    {
        return _behaviors.Exists(behavior => behavior.Registration == registration);
    }

    /// <summary>
    /// Lists the closed behavior types, outermost first.
    /// </summary>
    /// <returns>The closed behavior types.</returns>
    public ImmutableArray<Type> ClosedTypes() => [.. _behaviors.Select(behavior => behavior.ClosedType)];

    /// <summary>
    /// Creates an independent copy, which can change without affecting this list.
    /// </summary>
    /// <returns>The copy.</returns>
    public PlannedBehaviors Copy() => new(_behaviors);
}
