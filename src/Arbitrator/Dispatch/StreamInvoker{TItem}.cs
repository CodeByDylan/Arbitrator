using System.Collections.Immutable;

namespace Arbitrator.Dispatch;

/// <summary>
/// Runs the pipeline of one stream request type whose item type is known to the caller.
/// </summary>
/// <typeparam name="TItem">The item type.</typeparam>
internal abstract class StreamInvoker<TItem> : PipelineInvoker
{
    /// <summary>
    /// Creates an invoker for a handler and its behaviors.
    /// </summary>
    /// <param name="handlerType">The handler implementation type.</param>
    /// <param name="behaviorTypes">The closed behavior types, outermost first.</param>
    protected StreamInvoker(Type handlerType, ImmutableArray<Type> behaviorTypes)
        : base(handlerType, behaviorTypes)
    {
    }

    /// <summary>
    /// Resolves the behaviors and handler from a scope and invokes the outermost component for a request.
    /// </summary>
    /// <param name="request">The request, whose runtime type matches this invoker's request type.</param>
    /// <param name="services">The scope's service provider.</param>
    /// <param name="cancellationToken">The token passed to the outermost component.</param>
    /// <returns>The stream of the outermost component, not yet read.</returns>
    public abstract IAsyncEnumerable<TItem> Open(
        IStreamRequest<TItem> request,
        IServiceProvider services,
        CancellationToken cancellationToken);
}
