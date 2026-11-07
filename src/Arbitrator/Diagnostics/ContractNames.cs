namespace Arbitrator.Diagnostics;

/// <summary>
/// Formats Arbitrator's handler and behavior contracts for diagnostic messages.
/// </summary>
internal static class ContractNames
{
    /// <summary>
    /// Formats the handler contract for a request, using the single-parameter form for requests that implement
    /// <see cref="IRequest"/>.
    /// </summary>
    /// <param name="requestType">The request type.</param>
    /// <param name="responseType">The response type.</param>
    /// <returns>
    /// The formatted contract, such as <c>IRequestHandler&lt;Contoso.GetCustomer, Contoso.CustomerDto&gt;</c>.
    /// </returns>
    public static string Handler(Type requestType, Type responseType)
    {
        string request = TypeNameFormatter.Format(requestType);
        bool isRequestWithoutResponse = typeof(IRequest).IsAssignableFrom(requestType);

        return isRequestWithoutResponse
            ? $"IRequestHandler<{request}>"
            : $"IRequestHandler<{request}, {TypeNameFormatter.Format(responseType)}>";
    }

    /// <summary>
    /// Formats the behavior contract for a request.
    /// </summary>
    /// <param name="requestType">The request type.</param>
    /// <param name="responseType">The response type.</param>
    /// <returns>
    /// The formatted contract, such as <c>IPipelineBehavior&lt;Contoso.GetCustomer, Contoso.CustomerDto&gt;</c>.
    /// </returns>
    public static string Behavior(Type requestType, Type responseType)
    {
        return $"IPipelineBehavior<{TypeNameFormatter.Format(requestType)}, {TypeNameFormatter.Format(responseType)}>";
    }
}
