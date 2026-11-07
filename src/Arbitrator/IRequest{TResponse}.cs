namespace Arbitrator;

/// <summary>
/// Marks a request that is sent through <see cref="ISender"/> and handled by exactly one handler.
/// </summary>
/// <remarks>
/// A request type implements exactly one request interface. Requests are dispatched by their exact runtime type, so a
/// handler registered for a base type is never used for a derived type.
/// </remarks>
/// <typeparam name="TResponse">The type of the response the request produces.</typeparam>
public interface IRequest<TResponse>;
