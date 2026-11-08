# Arbitrator

[![CI](https://github.com/CodeByDylan/Arbitrator/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/CodeByDylan/Arbitrator/actions/workflows/ci.yml)

A small, opinionated, in-process mediator for .NET 11.

Arbitrator sends a typed request to exactly one handler, and publishes notifications to any number of handlers. Along
the way, a request passes through one ordered pipeline of behaviors, and every component is resolved from the caller's
dependency injection scope. The behavioral contract
is small enough to read in one sitting. `docs/contract.md`, in the source repository, specifies it in full.

- **Exactly one handler** for each concrete request type, selected by the request's exact runtime type.
- **Requests with and without a response.** Handlers of no-response requests return a plain `Task`.
- **Streaming requests.** A handler returns `IAsyncEnumerable<T>`, and stream behaviors wrap the whole read.
- **One pipeline abstraction for each kind of request.** Behaviors run in registration order, and the first
  registered is the outermost.
- **Notifications.** Zero or more handlers per notification type, run one after another in registration order.
- **Explicit registration** with actionable configuration errors, plus opt-in assembly scanning for handlers. Behaviors
  are always registered explicitly.
- **The caller's scope.** Arbitrator never creates a scope, caches an instance, or disposes a service.
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

## Notifications

A notification announces something that happened. Any number of handlers can react to it, including none:

```csharp
public sealed record CustomerDeactivated(Guid CustomerId, string Reason) : INotification;

public sealed class DeactivationEmailSender(IEmailQueue emails) : INotificationHandler<CustomerDeactivated>
{
    public Task HandleAsync(CustomerDeactivated notification, CancellationToken cancellationToken) =>
        emails.QueueDeactivationNoticeAsync(notification.CustomerId, cancellationToken);
}
```

Register handlers explicitly, and publish through the scoped `IPublisher`:

```csharp
builder.Services.AddArbitrator(arbitrator => arbitrator
    .AddHandler<DeactivateCustomerHandler>()
    .AddNotificationHandler<DeactivationAuditRecorder>()
    .AddNotificationHandler<DeactivationEmailSender>());

await publisher.PublishAsync(new CustomerDeactivated(customer.Id, reason), cancellationToken);
```

- **Order.** Handlers run one after another, in the order they were registered across all `AddArbitrator` calls. Each
  is awaited before the next starts, and every handler starts on the caller's synchronization context. Await
  publishing; never block on it with `.Wait()` or `.Result`, which deadlocks single-threaded contexts when several
  handlers are registered.
- **No handlers.** Publishing a notification nobody handles succeeds.
- **Failures.** The first exception stops publication and reaches the caller. Handlers that already ran keep their
  effects: publishing is not a transaction. If you need all-or-nothing, wrap the publish in your own transaction.
- **Cancellation.** The token is checked before the handlers are created and before each handler runs.
- **Exact type.** A notification reaches only the handlers registered for its exact runtime type, never those of its
  base class. Seal your notification types so this can't surprise you.
- **In-process only.** There is no parallel or fire-and-forget publishing, no durability, and no outbox. Request
  behaviors never wrap notification handlers.

## Streaming requests

A stream request produces its results one at a time, as the caller reads them. Its single handler returns
`IAsyncEnumerable<T>`:

```csharp
public sealed record SearchCustomers(string NamePrefix) : IStreamRequest<CustomerDto>;

public sealed class SearchCustomersHandler(ICustomerRepository customers)
    : IStreamRequestHandler<SearchCustomers, CustomerDto>
{
    public async IAsyncEnumerable<CustomerDto> HandleAsync(
        SearchCustomers request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (Customer customer in customers.SearchAsync(request.NamePrefix, cancellationToken))
        {
            yield return new CustomerDto(customer.Id, customer.Name, customer.IsActive);
        }
    }
}
```

Register it with `AddHandler`, like any handler, and read it through `ISender.StreamAsync`:

```csharp
await foreach (CustomerDto customer in sender.StreamAsync(new SearchCustomers("Ada"), cancellationToken))
{
    Console.WriteLine(customer.Name);
}
```

A stream behavior wraps the whole read, so code after its loop runs once the last item has been read. Register it
with `AddBehavior` or `AddOpenBehavior`, in the same order as every other behavior:

```csharp
public sealed class StreamLoggingBehavior<TRequest, TItem>(ILogger<StreamLoggingBehavior<TRequest, TItem>> logger)
    : IStreamPipelineBehavior<TRequest, TItem>
    where TRequest : IStreamRequest<TItem>
{
    public async IAsyncEnumerable<TItem> HandleAsync(
        TRequest request,
        StreamContinuation<TItem> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int count = 0;

        try
        {
            await foreach (TItem item in next(cancellationToken))
            {
                count++;
                yield return item;
            }
        }
        finally
        {
            logger.LogInformation("Read {Count} items of {RequestType}", count, typeof(TRequest).Name);
        }
    }
}
```

- **Lazy.** `StreamAsync` does nothing until the stream is read, and never throws. A missing handler, a null request,
  or a canceled token surfaces from the first `MoveNextAsync`, where the stream is read.
- **Each read is a dispatch.** Reading the same stream twice resolves new components and runs the handler twice.
- **Cancellation.** The token passed to `StreamAsync` and a token passed with `WithCancellation` are both honored. The
  token is checked once, when a read starts, and is then only forwarded, never checked between items.
- **Stay inside the scope.** A stream is read with the components of the scope that resolved `ISender`. Don't return
  it out of a `using` block that disposes that scope. In ASP.NET Core, returning it from an endpoint is fine: it is
  read during the response, inside the request's scope.
- **Behaviors stay apart.** `IPipelineBehavior` never wraps a stream handler, and `IStreamPipelineBehavior` never
  wraps any other handler. `next` may be invoked once, and the stream it returns may be read once.
- **Catching while yielding.** C# doesn't allow `yield return` inside a `try` block that has a `catch` clause. To
  translate exceptions, read the inner enumerator by hand and wrap only `MoveNextAsync` in the `try`:

  ```csharp
  await using IAsyncEnumerator<TItem> items = next(cancellationToken).GetAsyncEnumerator(cancellationToken);

  while (await MoveNextOrTranslateAsync(items))
  {
      yield return items.Current;
  }
  ```

  Here `MoveNextOrTranslateAsync` awaits `items.MoveNextAsync()` inside a `try` and throws your own exception from its
  `catch`.
- **Stop when you're done.** A handler may produce items forever. End the read with cancellation or by stopping,
  especially when you stream to a client.

## Assembly scanning

Instead of listing every handler, you can register all handlers of an assembly in one call. Scanning is opt-in, and
each call names exactly one assembly:

```csharp
builder.Services.AddArbitrator(arbitrator => arbitrator
    .AddOpenBehavior(typeof(LoggingBehavior<,>))
    .AddHandlersFromAssemblyContaining<GetCustomerHandler>());
```

- **What is registered.** Every concrete, non-generic class that implements `IRequestHandler<,>`, `IRequestHandler<>`,
  `IStreamRequestHandler<,>`, or `INotificationHandler<>`, whatever its visibility, including `internal` and nested
  classes.
  Every handler class in the assembly becomes reachable through `ISender` or `IPublisher`, so keep test-only or
  experimental handlers out of scanned assemblies, or exclude them with a filter.
- **Behaviors stay explicit.** A scan never registers behaviors, because their registration order is the pipeline
  order.
- **Order.** Scanned handlers are registered in ordinal order of their full type names. Scanned notification handlers
  therefore run in name order, after any handlers registered before the scan. To control the order of particular
  handlers, exclude them with a filter and register them explicitly.
- **Filter.** Both methods have an overload that takes a `Func<Type, bool>`. It sees each handler type once, and only
  the types it accepts are registered:

  ```csharp
  arbitrator.AddHandlersFromAssemblyContaining<GetCustomerHandler>(
      type => type.Namespace?.StartsWith("Contoso.Customers", StringComparison.Ordinal) == true);
  ```

- **Same rules as explicit registration.** Every scanned type is validated as `AddHandler` or
  `AddNotificationHandler` would validate it. A type that is already registered is an error, so scanning the same
  assembly twice fails, and so does registering a type explicitly that a scan also finds.
- **All or nothing.** If any type fails, the scan registers nothing. A scan that finds no handlers, a filter that
  excludes all of them, or an assembly whose types (or a handler's attributes) can't be loaded is a configuration error
  that names the assembly.

## The contract in brief

| Situation | Behavior |
| --- | --- |
| No handler for the request's runtime type | `ArbitratorConfigurationException` |
| Two handlers for one request type | `ArbitratorConfigurationException` at registration |
| Behavior order | Registration order across all `AddArbitrator` calls, with open and closed behaviors interleaved. The first registered is the outermost |
| A behavior returns without calling `next` | Inner behaviors and the handler don't run |
| `next` called twice | `InvalidOperationException` |
| Creating a stream with `StreamAsync` | Nothing happens until it is read. Every failure surfaces from the first `MoveNextAsync` |
| Reading a stream twice | Two dispatches: the handler runs twice |
| Stream behaviors | Wrap the whole read, in the shared behavior order. They never wrap other handlers, and request behaviors never wrap stream handlers |
| A stream behavior reads the stream from `next` twice | `InvalidOperationException` |
| No handler for a notification's runtime type | Publishing succeeds |
| A notification handler throws | Later handlers don't run, and the same exception instance reaches the caller |
| Handler or behavior throws | The same exception instance reaches the caller |
| Token already canceled | The send, or the stream read, is canceled before anything is constructed. A stream observes both its `StreamAsync` token and the read's token |
| Cancellation during the send or read | Forwarded only. Handlers decide how to observe it |
| Lifetimes | `ISender` and `IPublisher` are scoped. Handlers, notification handlers, and behaviors are transient |
| Disposal | The scope disposes handlers and behaviors when it ends |
| Registration after the provider was built | `ArbitratorConfigurationException` when that provider resolves `ISender` or `IPublisher` |
| Registration on a read-only collection | `ArbitratorConfigurationException`, and nothing is registered |
| A scanned type is invalid or already registered | `ArbitratorConfigurationException`, and the scan registers nothing |

Turn on `ValidateOnBuild` and `ValidateScopes` for your container. Container validation then checks the dependencies
of every handler, notification handler, and behavior that applies to a registered request. It also catches a
singleton that captures `ISender` or `IPublisher`.

Arbitrator's exception messages are diagnostics for developers. They name types, never request contents. Even so,
don't return them to clients: they reveal your application's namespaces and type names.

## Trimming and Native AOT

Registration closes generic types at runtime and inspects request types through reflection. For that reason,
`AddHandler` and `AddOpenBehavior` are marked `RequiresDynamicCode` and `RequiresUnreferencedCode`,
`AddNotificationHandler` is marked `RequiresDynamicCode`, and Native AOT isn't supported yet. The assembly scanning
methods carry both attributes: they discover types at runtime, and trimming can remove handler types that nothing else
references.

## License

Apache-2.0. See [LICENSE](LICENSE).

## Releases

Every pull request is built and tested by CI. Publishing a GitHub Release from a `v` tag publishes that version to
nuget.org through Trusted Publishing; `docs/releasing.md` in the source repository describes the procedure.
