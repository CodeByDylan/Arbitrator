using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Scopes;

/// <summary>
/// Specifies that handlers and behaviors are resolved from the scope that resolved the sender.
/// </summary>
public sealed class ScopeTests
{
    [Test]
    public async Task Handler_receives_the_scoped_services_of_the_senders_scope()
    {
        await using TestHost host = CreateHost();

        await host.Sender.SendAsync(new Outer(SendInner: false));

        ScopedDependency expected = host.Scope.ServiceProvider.GetRequiredService<ScopedDependency>();
        await Assert.That(host.Journal.Captured<ScopedDependency>().Single()).IsSameReferenceAs(expected);
    }

    [Test]
    public async Task Senders_of_different_scopes_use_different_scoped_services()
    {
        await using TestHost host = CreateHost();
        await using AsyncServiceScope otherScope = host.Provider.CreateAsyncScope();

        await host.Sender.SendAsync(new Outer(SendInner: false));
        await otherScope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new Outer(SendInner: false));

        IReadOnlyList<ScopedDependency> dependencies = host.Journal.Captured<ScopedDependency>();
        await Assert.That(dependencies[0]).IsNotSameReferenceAs(dependencies[1]);
        await Assert.That(dependencies[1])
            .IsSameReferenceAs(otherScope.ServiceProvider.GetRequiredService<ScopedDependency>());
    }

    [Test]
    public async Task Nested_send_runs_its_own_pipeline_in_the_same_scope()
    {
        await using TestHost host = CreateHost();

        await host.Sender.SendAsync(new Outer(SendInner: true));

        IReadOnlyList<ScopedDependency> dependencies = host.Journal.Captured<ScopedDependency>();
        await Assert.That(dependencies[0]).IsSameReferenceAs(dependencies[1]);
        await Expect.EntriesAsync(host.Journal, "behavior:Outer", "behavior:Inner", "inner handler");
    }

    [Test]
    public async Task Resolving_the_sender_from_the_root_provider_fails_when_scopes_are_validated()
    {
        await using TestHost host = CreateHost();

        InvalidOperationException exception = Expect.ThrowsExactly<InvalidOperationException>(
            () => host.Provider.GetRequiredService<ISender>());

        await Assert.That(exception.Message).Contains("from root provider");
    }

    private static TestHost CreateHost()
    {
        return TestHost.CreateWithServices(services => services
            .AddScoped<ScopedDependency>()
            .AddArbitrator(arbitrator => arbitrator
                .AddOpenBehavior(typeof(RequestNameBehavior<,>))
                .AddHandler<OuterHandler>()
                .AddHandler<InnerHandler>()));
    }

    public sealed class ScopedDependency;

    public sealed record Outer(bool SendInner) : IRequest;

    public sealed record Inner : IRequest;

    public sealed class RequestNameBehavior<TRequest, TResponse>(Journal journal)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request,
            RequestContinuation<TResponse> next,
            CancellationToken cancellationToken)
        {
            journal.Record($"behavior:{typeof(TRequest).Name}");
            return next(cancellationToken);
        }
    }

    public sealed class OuterHandler(ISender sender, ScopedDependency dependency, Journal journal)
        : IRequestHandler<Outer>
    {
        public async Task HandleAsync(Outer request, CancellationToken cancellationToken)
        {
            journal.Capture(dependency);

            if (request.SendInner)
            {
                await sender.SendAsync(new Inner(), cancellationToken);
            }
        }
    }

    public sealed class InnerHandler(ScopedDependency dependency, Journal journal) : IRequestHandler<Inner>
    {
        public Task HandleAsync(Inner request, CancellationToken cancellationToken)
        {
            journal.Record("inner handler");
            journal.Capture(dependency);
            return Task.CompletedTask;
        }
    }
}
