using System.Reflection;
using Arbitrator.Tests.Scanning.Fixtures;
using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Scanning;

/// <summary>
/// Specifies the errors an assembly scan reports, and that discovered types report the same errors as explicit
/// registrations.
/// </summary>
public sealed class ScanningDiagnosticsTests
{
    [Test]
    public async Task Unloadable_types_are_reported_with_the_first_five_distinct_loader_messages()
    {
        Exception?[] loaderExceptions =
        [
            new FileNotFoundException("Missing A."),
            null,
            new FileNotFoundException("Missing A."),
            new TypeLoadException("Missing B."),
            new TypeLoadException("Missing C."),
            new TypeLoadException("Missing D."),
            new TypeLoadException("Missing E."),
            new TypeLoadException("Missing F."),
            new TypeLoadException("Missing G."),
        ];
        var failure = new ReflectionTypeLoadException([], loaderExceptions);
        StubAssembly assembly = StubAssembly.FailingToLoad("Contoso.Broken", failure);

        ArbitratorConfigurationException exception = Scan(assembly);

        await Assert.That(exception.Message).IsEqualTo(
            "Assembly 'Contoso.Broken' cannot be scanned because some of its types could not be loaded: "
            + "Missing A. Missing B. Missing C. Missing D. Missing E. (and 2 more)");
        await Assert.That(exception.InnerException).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task Unloadable_types_with_few_loader_messages_list_them_all()
    {
        var failure = new ReflectionTypeLoadException([], [new TypeLoadException("Missing A.")]);
        StubAssembly assembly = StubAssembly.FailingToLoad("Contoso.Broken", failure);

        ArbitratorConfigurationException exception = Scan(assembly);

        await Assert.That(exception.Message).EndsWith("could not be loaded: Missing A.");
    }

    [Test]
    public async Task Unloadable_types_without_loader_messages_point_to_the_inner_exception()
    {
        var failure = new ReflectionTypeLoadException([], [null]);
        StubAssembly assembly = StubAssembly.FailingToLoad("Contoso.Broken", failure);

        ArbitratorConfigurationException exception = Scan(assembly);

        await Assert.That(exception.Message).IsEqualTo(
            "Assembly 'Contoso.Broken' cannot be scanned because some of its types could not be loaded. "
            + "The inner exception has the details.");
        await Assert.That(exception.InnerException).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task Handler_whose_attributes_cannot_be_loaded_is_reported_with_the_assembly_and_the_type()
    {
        var handlerType = new AttributeLoadFailingType(
            typeof(AlphaNoticeHandler),
            new FileNotFoundException("Could not load file or assembly 'Contoso.Annotations'."));
        StubAssembly assembly = StubAssembly.Defining("Contoso.Annotated", handlerType);

        ArbitratorConfigurationException exception = Scan(assembly);

        await Assert.That(exception.Message).IsEqualTo(
            "Assembly 'Contoso.Annotated' cannot be scanned because the attributes of handler type "
            + $"'{Names.Of<AlphaNoticeHandler>()}' could not be loaded: "
            + "Could not load file or assembly 'Contoso.Annotations'.");
        await Assert.That(exception.InnerException).IsSameReferenceAs(handlerType.Failure);
    }

    [Test]
    public async Task Assembly_without_handler_types_is_reported_by_name()
    {
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandlersFromAssemblyContaining<ISender>()));

        await Assert.That(exception.Message)
            .Contains("Assembly 'Arbitrator' contains no request handler or notification handler types.");
    }

    [Test]
    public async Task Filter_that_excludes_every_handler_type_is_reported_with_the_excluded_count()
    {
        StubAssembly assembly = StubAssembly.Defining(
            "Contoso.Events",
            typeof(AlphaNoticeHandler),
            typeof(BetaNoticeHandler),
            typeof(ScanNotice));
        var services = new ServiceCollection();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandlersFromAssembly(assembly, _ => false)));

        await Assert.That(exception.Message)
            .IsEqualTo("The filter excluded all 2 handler types found in assembly 'Contoso.Events', so nothing was "
                + "registered.");
    }

    [Test]
    public async Task Invalid_discovered_handler_is_reported_exactly_as_its_explicit_registration()
    {
        StubAssembly assembly = StubAssembly.Defining("Contoso.Queries", typeof(QueryBaseHandler));
        var services = new ServiceCollection();

        ArbitratorConfigurationException scanned = Scan(assembly);
        ArbitratorConfigurationException registered = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandler<QueryBaseHandler>()));

        await Assert.That(scanned.Message).IsEqualTo(registered.Message);
        await Assert.That(scanned.InnerException).IsNull();
    }

    private static ArbitratorConfigurationException Scan(Assembly assembly)
    {
        var services = new ServiceCollection();

        return Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => services.AddArbitrator(arbitrator => arbitrator.AddHandlersFromAssembly(assembly)));
    }

    public abstract record QueryBase : IRequest<string>;

    public sealed class QueryBaseHandler : IRequestHandler<QueryBase, string>
    {
        public Task<string> HandleAsync(QueryBase request, CancellationToken cancellationToken)
        {
            return Task.FromResult(string.Empty);
        }
    }
}
