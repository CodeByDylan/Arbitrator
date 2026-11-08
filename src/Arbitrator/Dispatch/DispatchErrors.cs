using Arbitrator.Diagnostics;

namespace Arbitrator.Dispatch;

/// <summary>
/// Creates the exceptions that report failures detected while sending a request or publishing a notification.
/// </summary>
internal static class DispatchErrors
{
    /// <summary>
    /// Reports a request type that has no registered handler.
    /// </summary>
    /// <param name="kind">The kind of pipeline the caller dispatched through.</param>
    /// <param name="requestType">The runtime type of the request.</param>
    /// <param name="responseType">The response or item type requested by the caller.</param>
    /// <param name="handledBaseType">A base type of the request that has a handler, if any.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException MissingHandler(
        PipelineKind kind,
        Type requestType,
        Type responseType,
        Type? handledBaseType)
    {
        string request = TypeNameFormatter.Format(requestType);
        string contract = ContractNames.Handler(kind.HandlerContract, requestType, responseType);
        string message = $"No handler is registered for {kind.RequestDescription} '{request}'. Register a class "
            + $"implementing {contract} with AddHandler<THandler>(). "
            + "Handlers are matched to the exact runtime type of the request.";

        if (handledBaseType is not null)
        {
            message += $" A handler is registered for base type '{TypeNameFormatter.Format(handledBaseType)}'; "
                + $"handlers for base types are not used for '{request}'.";
        }

        return new(message);
    }

    /// <summary>
    /// Reports handlers and behaviors that a service provider cannot resolve because they were registered after the
    /// provider was built.
    /// </summary>
    /// <param name="componentTypes">The handler and behavior types missing from the provider.</param>
    /// <returns>The exception to throw.</returns>
    public static ArbitratorConfigurationException ComponentsMissingFromProvider(IReadOnlyList<Type> componentTypes)
    {
        string components = string.Join(", ", componentTypes.Select(type => $"'{TypeNameFormatter.Format(type)}'"));

        return new($"These handlers and behaviors were registered after this service provider was built, so it "
            + $"cannot resolve them: {components}. Complete all AddArbitrator calls before building the service "
            + "provider.");
    }

    /// <summary>
    /// Reports a behavior that invoked its continuation more than once.
    /// </summary>
    /// <param name="behaviorType">The behavior that received the continuation.</param>
    /// <param name="requestType">The request type being handled.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException ContinuationInvokedTwice(Type behaviorType, Type requestType)
    {
        return new($"Behavior '{TypeNameFormatter.Format(behaviorType)}' invoked its continuation more than once "
            + $"while handling '{TypeNameFormatter.Format(requestType)}'. "
            + "A continuation may be invoked at most once.");
    }

    /// <summary>
    /// Reports a handler that returned a null task.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <param name="requestType">The request type being handled.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException NullHandlerTask(Type handlerType, Type requestType)
    {
        return new($"Handler '{TypeNameFormatter.Format(handlerType)}' returned a null Task "
            + $"while handling '{TypeNameFormatter.Format(requestType)}'.");
    }

    /// <summary>
    /// Reports a behavior that returned a null task.
    /// </summary>
    /// <param name="behaviorType">The behavior type.</param>
    /// <param name="requestType">The request type being handled.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException NullBehaviorTask(Type behaviorType, Type requestType)
    {
        return new($"Behavior '{TypeNameFormatter.Format(behaviorType)}' returned a null Task "
            + $"while handling '{TypeNameFormatter.Format(requestType)}'.");
    }

    /// <summary>
    /// Reports a behavior that read the stream returned by its continuation more than once.
    /// </summary>
    /// <param name="behaviorType">The behavior that received the continuation.</param>
    /// <param name="requestType">The request type being handled.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException ContinuationStreamReadTwice(Type behaviorType, Type requestType)
    {
        return new($"Behavior '{TypeNameFormatter.Format(behaviorType)}' read the stream returned by its "
            + $"continuation more than once while handling '{TypeNameFormatter.Format(requestType)}'. "
            + "A continuation's stream may be read at most once.");
    }

    /// <summary>
    /// Reports a stream handler that returned a null stream.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <param name="requestType">The request type being handled.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException NullHandlerStream(Type handlerType, Type requestType)
    {
        return new($"Handler '{TypeNameFormatter.Format(handlerType)}' returned a null stream "
            + $"while handling '{TypeNameFormatter.Format(requestType)}'.");
    }

    /// <summary>
    /// Reports a stream behavior that returned a null stream.
    /// </summary>
    /// <param name="behaviorType">The behavior type.</param>
    /// <param name="requestType">The request type being handled.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException NullBehaviorStream(Type behaviorType, Type requestType)
    {
        return new($"Behavior '{TypeNameFormatter.Format(behaviorType)}' returned a null stream "
            + $"while handling '{TypeNameFormatter.Format(requestType)}'.");
    }
}
