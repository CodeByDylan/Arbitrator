using Arbitrator.Tests.Support;

namespace Arbitrator.Tests.Diagnostics;

/// <summary>
/// Specifies how diagnostic messages write types and the handler contract a missing handler must implement.
/// </summary>
public sealed class DiagnosticMessageTests
{
    private const string Prefix = "Arbitrator.Tests.Diagnostics.DiagnosticMessageTests";

    [Test]
    public async Task Type_nested_in_a_generic_type_is_written_with_its_enclosing_type_arguments()
    {
        string message = await MissingHandlerMessageAsync(new Outer<int>.Query());

        await Assert.That(message).Contains($"'{Prefix}.Outer<int>.Query'");
    }

    [Test]
    public async Task Arrays_nullable_values_and_keyword_types_are_written_as_in_csharp()
    {
        string message = await MissingHandlerMessageAsync(new Grid());

        await Assert.That(message).Contains($"IRequestHandler<{Prefix}.Grid, int?[][,]>");
    }

    [Test]
    public async Task Generic_request_type_is_written_with_its_type_arguments()
    {
        string message = await MissingHandlerMessageAsync(new Page<Guid>());

        await Assert.That(message).Contains($"IRequestHandler<{Prefix}.Page<System.Guid>, string>");
    }

    [Test]
    public async Task Missing_handler_for_a_request_without_response_suggests_the_single_parameter_contract()
    {
        string message = await MissingHandlerMessageAsync(new Notify("hello"));

        await Assert.That(message).Contains($"IRequestHandler<{Names.Of<Notify>()}> with");
    }

    [Test]
    public async Task Missing_handler_for_a_request_declared_with_unit_suggests_the_two_parameter_contract()
    {
        string message = await MissingHandlerMessageAsync(new UnitRequest());

        await Assert.That(message).Contains($"IRequestHandler<{Prefix}.UnitRequest, Arbitrator.Unit>");
    }

    private static async Task<string> MissingHandlerMessageAsync<TResponse>(IRequest<TResponse> request)
    {
        await using TestHost host = TestHost.Create(_ => { });

        ArbitratorConfigurationException exception =
            await Expect.ThrowsExactlyAsync<ArbitratorConfigurationException>(() => host.Sender.SendAsync(request));

        return exception.Message;
    }

    public static class Outer<T>
    {
        public sealed record Query : IRequest<string>;
    }

    public sealed record Grid : IRequest<int?[][,]>;

    public sealed record Page<T> : IRequest<string>;

    public sealed record UnitRequest : IRequest<Unit>;
}
