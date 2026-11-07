namespace Arbitrator;

/// <summary>
/// Marks a request that produces no response.
/// </summary>
/// <remarks>
/// Pipeline behaviors observe such a request with <see cref="Unit"/> as its response type.
/// </remarks>
public interface IRequest : IRequest<Unit>;
