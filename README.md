# Arbitrator

[![CI](https://github.com/CodeByDylan/Arbitrator/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/CodeByDylan/Arbitrator/actions/workflows/ci.yml)

A small, opinionated, in-process mediator for .NET 11.

Arbitrator sends a typed request to exactly one handler. Along the way, the request passes through one ordered pipeline
of behaviors, and every component is resolved from the caller's dependency injection scope. The behavioral contract
is small enough to read in one sitting. `DESIGN.md`, in the root of the source repository, specifies it in full.

- **Exactly one handler** for each concrete request type, selected by the request's exact runtime type.
- **Requests with and without a response.** Handlers of no-response requests return a plain `Task`.
- **One pipeline abstraction.** Behaviors run in registration order, and the first registered is the outermost.
- **Explicit registration** with actionable configuration errors. There is no assembly scanning.
- **The caller's scope.** Arbitrator never creates a scope, caches an instance, or disposes anything.
- **Unwrapped exceptions.** Exceptions reach the caller as the same instance, and cancellation is checked once and
  then forwarded.

Arbitrator has no package dependencies. The dependency injection abstractions it uses ship with .NET 11.

## Quick start

Define a request and its handler:

```csharp
public sealed record GetCustomer(Guid Id) : IRequest<CustomerDto?>;

public sealed class GetCustomerHandler(ICustomerRepository customers)
    : IRequestHandler<GetCustomer, CustomerDto?>
{
    public async Task<CustomerDto?> HandleAsync(GetCustomer request, CancellationToken cancellationToken)
    {
        Customer? customer = await customers.FindAsync(request.Id, cancellationToken);
        return customer is null ? null : new CustomerDto(customer.Id, customer.Name, customer.IsActive);
    }
}
```

A request without a response implements `IRequest`, and its handler returns `Task`:

```csharp
public sealed record DeactivateCustomer(Guid Id, string Reason) : IRequest;

public sealed class DeactivateCustomerHandler(ICustomerRepository customers)
    : IRequestHandler<DeactivateCustomer>
{
    public async Task HandleAsync(DeactivateCustomer request, CancellationToken cancellationToken)
    {
        Customer customer = await customers.FindAsync(request.Id, cancellationToken)
            ?? throw new CustomerNotFoundException(request.Id);

        customer.Deactivate(request.Reason);
        await customers.SaveChangesAsync(cancellationToken);
    }
}
```

Behaviors wrap the handler. Constraints on an open behavior's type parameters decide which requests it applies to:

```csharp
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IValidatable
{
    public Task<TResponse> HandleAsync(
        TRequest request,
        RequestContinuation<TResponse> next,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = request.Validate();

        if (errors.Count > 0)
        {
            throw new RequestValidationException(typeof(TRequest).Name, errors);
        }

        return next(cancellationToken);
    }
}
```

Register everything explicitly:

```csharp
builder.Services.AddScoped<ICustomerRepository, SqlCustomerRepository>();

builder.Services.AddArbitrator(arbitrator => arbitrator
    .AddOpenBehavior(typeof(LoggingBehavior<,>))
    .AddOpenBehavior(typeof(ValidationBehavior<,>))
    .AddHandler<GetCustomerHandler>()
    .AddHandler<DeactivateCustomerHandler>());
```

Send requests through the scoped `ISender`. The response type is inferred from the request:

```csharp
app.MapGet("/customers/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
{
    CustomerDto? customer = await sender.SendAsync(new GetCustomer(id), cancellationToken);
    return customer is null ? Results.NotFound() : Results.Ok(customer);
});
```

Code that runs outside a request, such as a hosted service, creates its own scope and resolves `ISender` from it.

## The contract in brief

| Situation | Behavior |
| --- | --- |
| No handler for the request's runtime type | `ArbitratorConfigurationException` |
| Two handlers for one request type | `ArbitratorConfigurationException` at registration |
| Behavior order | Registration order across all `AddArbitrator` calls, with open and closed behaviors interleaved. The first registered is the outermost |
| A behavior returns without calling `next` | Inner behaviors and the handler don't run |
| `next` called twice | `InvalidOperationException` |
| Handler or behavior throws | The same exception instance reaches the caller |
| Token already canceled | The send is canceled before anything is constructed |
| Cancellation during the send | Forwarded only. Handlers decide how to observe it |
| Lifetimes | `ISender` is scoped. Handlers and behaviors are transient |
| Disposal | The scope disposes handlers and behaviors when it ends |
| Registration after the provider was built | `ArbitratorConfigurationException` when that provider resolves `ISender` |
| Registration on a read-only collection | `ArbitratorConfigurationException`, and nothing is registered |

Turn on `ValidateOnBuild` and `ValidateScopes` for your container. Container validation then checks the dependencies
of every handler and every behavior that applies to a registered request. It also catches a singleton that captures
`ISender`.

Arbitrator's exception messages are diagnostics for developers. They name types, never request contents. Even so,
don't return them to clients: they reveal your application's namespaces and type names.

## Trimming and Native AOT

Registration closes generic types at runtime and inspects request types through reflection. For that reason,
`AddHandler` and `AddOpenBehavior` are marked `RequiresDynamicCode` and `RequiresUnreferencedCode`, and Native AOT
isn't supported in v0.1.

## License

Apache-2.0. See [LICENSE](LICENSE).

## Releases

Every pull request is built and tested by CI. Publishing a GitHub Release from a `v` tag publishes that version to
nuget.org through Trusted Publishing; `docs/releasing.md` in the source repository describes the procedure.
