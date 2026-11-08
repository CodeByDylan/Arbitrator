namespace Arbitrator;

/// <summary>
/// Marks a request that produces a stream of items, read through <see cref="ISender"/> and handled by exactly one
/// stream handler.
/// </summary>
/// <remarks>
/// A request type implements exactly one request interface. Requests are dispatched by their exact runtime type, so a
/// handler registered for a base type is never used for a derived type.
/// </remarks>
/// <typeparam name="TItem">The type of the items the request produces.</typeparam>
public interface IStreamRequest<TItem>;
