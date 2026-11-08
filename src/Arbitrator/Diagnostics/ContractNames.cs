namespace Arbitrator.Diagnostics;

/// <summary>
/// Formats Arbitrator's handler and behavior contracts for diagnostic messages.
/// </summary>
internal static class ContractNames
{
    /// <summary>
    /// Formats a handler contract for a request, using the single-parameter form for requests that implement
    /// <see cref="IRequest"/>.
    /// </summary>
    /// <param name="handlerContract">The generic definition of the handler contract.</param>
    /// <param name="requestType">The request type.</param>
    /// <param name="responseType">The response type, or the item type of a stream request.</param>
    /// <returns>
    /// The formatted contract, such as <c>IRequestHandler&lt;Contoso.GetCustomer, Contoso.CustomerDto&gt;</c>.
    /// </returns>
    public static string Handler(Type handlerContract, Type requestType, Type responseType)
    {
        bool isRequestWithoutResponse = handlerContract == typeof(IRequestHandler<,>)
            && typeof(IRequest).IsAssignableFrom(requestType);

        return isRequestWithoutResponse
            ? $"IRequestHandler<{TypeNameFormatter.Format(requestType)}>"
            : Closed(handlerContract, requestType, responseType);
    }

    /// <summary>
    /// Formats a behavior contract for a request.
    /// </summary>
    /// <param name="behaviorContract">The generic definition of the behavior contract.</param>
    /// <param name="requestType">The request type.</param>
    /// <param name="responseType">The response type, or the item type of a stream request.</param>
    /// <returns>
    /// The formatted contract, such as <c>IPipelineBehavior&lt;Contoso.GetCustomer, Contoso.CustomerDto&gt;</c>.
    /// </returns>
    public static string Behavior(Type behaviorContract, Type requestType, Type responseType)
    {
        return Closed(behaviorContract, requestType, responseType);
    }

    /// <summary>
    /// Formats a two-parameter contract closed over a request type and a response or item type.
    /// </summary>
    /// <param name="contractDefinition">The generic definition of the contract.</param>
    /// <param name="requestType">The request type.</param>
    /// <param name="responseType">The response or item type.</param>
    /// <returns>The formatted contract, without the namespace of the contract itself.</returns>
    private static string Closed(Type contractDefinition, Type requestType, Type responseType)
    {
        string name = contractDefinition.Name[..contractDefinition.Name.IndexOf('`', StringComparison.Ordinal)];
        return $"{name}<{TypeNameFormatter.Format(requestType)}, {TypeNameFormatter.Format(responseType)}>";
    }
}
