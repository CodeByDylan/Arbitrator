using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Support;

/// <summary>
/// A service provider built with full container validation, a scope, and the scope's sender, owned by one test.
/// </summary>
public sealed class TestHost : IAsyncDisposable
{
    /// <summary>
    /// Creates a host from an open scope of a provider.
    /// </summary>
    /// <param name="provider">The root provider.</param>
    /// <param name="scope">The scope that tests send requests from.</param>
    private TestHost(ServiceProvider provider, AsyncServiceScope scope)
    {
        Provider = provider;
        Scope = scope;
    }

    /// <summary>
    /// Gets the options that enable every container validation.
    /// </summary>
    public static ServiceProviderOptions StrictValidation => new() { ValidateOnBuild = true, ValidateScopes = true };

    /// <summary>
    /// Gets the root provider.
    /// </summary>
    public ServiceProvider Provider { get; }

    /// <summary>
    /// Gets the scope that tests send requests from.
    /// </summary>
    public AsyncServiceScope Scope { get; }

    /// <summary>
    /// Gets the sender of <see cref="Scope"/>.
    /// </summary>
    public ISender Sender => Scope.ServiceProvider.GetRequiredService<ISender>();

    /// <summary>
    /// Gets the journal shared by the handlers and behaviors of this host.
    /// </summary>
    public Journal Journal => Provider.GetRequiredService<Journal>();

    /// <summary>
    /// Creates a host with a single Arbitrator registration call.
    /// </summary>
    /// <param name="configure">Registers handlers and behaviors.</param>
    /// <returns>The host.</returns>
    public static TestHost Create(Action<ArbitratorBuilder> configure)
    {
        return CreateWithServices(services => services.AddArbitrator(configure));
    }

    /// <summary>
    /// Creates a host whose services, including Arbitrator registrations, are added by the test.
    /// </summary>
    /// <param name="configureServices">Adds services to the collection.</param>
    /// <returns>The host.</returns>
    public static TestHost CreateWithServices(Action<IServiceCollection> configureServices)
    {
        ServiceCollection services = [];
        services.AddSingleton<Journal>();
        configureServices(services);

        ServiceProvider provider = services.BuildServiceProvider(StrictValidation);
        return new TestHost(provider, provider.CreateAsyncScope());
    }

    /// <summary>
    /// Disposes the scope and the provider.
    /// </summary>
    /// <returns>A task that completes when both are disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        await Scope.DisposeAsync();
        await Provider.DisposeAsync();
    }
}
