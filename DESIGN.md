# Arbitrator: Design for v0.1

| | |
| --- | --- |
| Status | Approved; v0.1 implemented |
| Target | `net11.0`, C# 15 |
| Date | 2026-11-07 |
| Package | `Arbitrator` (single package) |

---

## 1. Executive summary

Arbitrator is a small, opinionated, in-process mediator for .NET 11. It sends a typed request to exactly one handler. Along the way the request passes through an ordered pipeline of behaviors, and every component is resolved from the caller's Microsoft.Extensions.DependencyInjection scope. Arbitrator is for developers who want request/handler separation and a single cross-cutting pipeline, but don't want a large framework. They should be able to read the entire behavioral contract in one sitting and test it.

**Recommended v0.1 scope, and nothing more:**

- Requests that return a response (`IRequest<TResponse>`) and requests that don't (`IRequest`), sent through `ISender`.
- Exactly one handler per concrete request type, chosen by the request's exact runtime type.
- One pipeline abstraction, `IPipelineBehavior<TRequest, TResponse>`. It supports closed behaviors, open-generic behaviors, and constrained open-generic behaviors. Registration order is the only ordering rule.
- Explicit registration through a single `AddArbitrator` entry point. Arbitrator validates configuration at registration time and works with the container's own validation.
- One custom exception type for configuration errors. Every other exception passes through untouched.
- One NuGet package with no package dependencies. On .NET 11, `Microsoft.Extensions.DependencyInjection.Abstractions` ships in the shared framework.

Notifications are deferred to v0.2 (§13). Native AOT support isn't claimed (§5.11).

### Current state

The repository holds an empty `Arbitrator.slnx` and a `.gitignore`. It has no projects yet. The installed SDK is `11.0.100-rc.3.26522.148`. On this date .NET 11 isn't generally available, so the newest `Microsoft.Extensions.*` packages are `11.0.0-rc.3.26522.148`. The NuGet package ID `Arbitrator` is unclaimed.

---

## 2. Goals, non-goals, and design principles

### Goals

1. Typed dispatch of a request to exactly one handler, with the response type inferred at the call site.
2. Requests with and without a response. Callers and handlers of no-response requests never touch a placeholder value.
3. A single, composable request pipeline that wraps both kinds of request.
4. Integration with Microsoft.Extensions.DependencyInjection (MS.DI) that uses the caller's scope and leaves lifetimes and disposal to the container.
5. Explicit registration with actionable diagnostics, reported at the earliest stage that can detect the problem.
6. Correct cancellation forwarding, unwrapped exception propagation, and predictable scope behavior.
7. A small, documented behavioral contract (§7) with a test for every row (§10).

### Non-goals for v0.1

Streaming requests. Notifications (deferred). Source generators. Assembly scanning. Parallel notification publishing. Built-in retries, validation, logging, transactions, or EF Core integration. Polymorphic handler resolution. Containers other than MS.DI. Object-based dispatch (`Send(object)`). A prescribed result or error type. Distributed messaging, persistence, or outbox. MediatR API compatibility. A Native AOT compatibility guarantee.

### Design principles

These are the opinions that set Arbitrator apart from other mediators:

1. **One way to do each thing.** Each request has one handler, there's one pipeline abstraction, one registration entry point, and one ordering rule.
2. **Explicit over discovered.** Only types named in `AddArbitrator` take part in dispatch. `IRequestHandler<,>` services registered directly on the `IServiceCollection` are ignored.
3. **Fail at the earliest stage that can know.** The stages, in order, are registration, container validation, first sender resolution, and dispatch. §6.5 lists which failures each stage can detect.
4. **The caller owns the scope, and DI owns lifetimes.** Arbitrator never creates a scope, never disposes anything, and never caches service instances.
5. **Stay out of the exception path.** Arbitrator never catches, wraps, logs, translates, or retries exceptions from behaviors, handlers, or DI activation.
6. **Forward cancellation without interpreting it.** Arbitrator checks the token once at the entry point and otherwise only passes it along.
7. **No static mutable state.** Every piece of state belongs to a service collection or a service provider, so tests and multiple hosts in one process stay isolated.
8. **Start strict, relax later.** Relaxing a strict rule later doesn't break anyone, while tightening a loose one does. That's why v0.1 guards the continuation, uses transient-only lifetimes, and rejects duplicates.

---

## 3. Key decisions

| # | Decision | Chosen approach | Rationale | Main trade-off | Settle before implementation? |
| --- | --- | --- | --- | --- | --- |
| 1 | Method naming | `SendAsync` and `HandleAsync` | Follows the owner's C# standard that async methods end in `Async`. | Longer than the conceptual `Send`/`Handle`, and differs from MediatR habits. | **Yes, before M1** |
| 2 | Task type | `Task` and `Task<TResponse>` everywhere | `ValueTask` can only be awaited once and can't be safely stored or combined. Behaviors would push those rules onto every author. Handlers usually do I/O, and runtime async in .NET 11 makes `Task` cheaper. | One `Task` allocation when a handler completes synchronously. | Yes |
| 3 | No-response requests | `IRequest` inherits `IRequest<Unit>`. `IRequestHandler<TRequest>` returns `Task` and bridges to `Unit` with a default interface implementation. `SendAsync(IRequest)` returns `Task`. | One dispatch path and one pipeline for both kinds. Callers and handlers never see `Unit`. | `Unit` is public, and behaviors see it as `TResponse`. | Yes |
| 4 | Response inference | `SendAsync<TResponse>(IRequest<TResponse>)`. `IRequest<TResponse>` is invariant. A request type implements exactly one request interface. | C# infers `TResponse` from the request's single `IRequest<>` implementation (§4.3). | Request types that implement two request interfaces are rejected when their handler is registered. | Yes |
| 5 | Request type identity | Arbitrator looks up `request.GetType()` in an immutable table and requires an exact match. | References typed as an interface or base type still reach the concrete handler, and the lookup never falls back to a base type. | A derived request type needs its own handler. | Yes |
| 6 | Pipeline shape | `HandleAsync(request, next, cancellationToken)`, where `next` is `RequestContinuation<TResponse>(CancellationToken)`. | Token forwarding is explicit, and a behavior can substitute a token. | Behavior authors must pass a token to `next`. | Yes |
| 7 | Continuation reuse | `next` may be invoked at most once. A second call throws `InvalidOperationException`. | Prevents accidental duplicate side effects. Allowing reuse later is non-breaking. | Retry behaviors can't live inside the pipeline. | Yes |
| 8 | Behavior ordering | One global registration order covers open and closed behaviors across all `AddArbitrator` calls. The first registered behavior is outermost. | A single rule with no priorities or groups. | Order across modules depends on the order of the modules' `AddArbitrator` calls. | Yes |
| 9 | Constrained open behaviors | Supported. A constrained behavior applies to a request exactly when the runtime accepts its generic constraints for that request and response type. | Behaviors can target requests through marker interfaces without a separate filtering framework. | Compile-time-only constraints (`notnull`, nullable annotations) aren't evaluated. | Yes |
| 10 | Lifetimes | `ISender` is scoped. Handlers and behaviors are transient, and v0.1 doesn't let you change that. | No captive dependencies and no state shared between sends. Scope validation catches a sender captured in a singleton. | No singleton-handler optimization yet. Adding one later is non-breaking. | Yes |
| 11 | Internal DI registration | Handlers and behaviors are registered as their own implementation types under a private Arbitrator service key. Open behaviors are registered as closed instantiations for each registered request. | Isolated from application registrations, so the container can never silently pick the last of several. Container validation covers every pipeline component. | Requires a provider that supports keyed services (MS.DI does). The number of descriptors grows with handlers × open behaviors. | Yes |
| 12 | Duplicates | Duplicate handler contracts, handler types, behavior types, and behaviors that would run twice for one request are all errors. | Arbitrator never silently chooses. | An accidental double module registration fails loudly. | Yes |
| 13 | Instance resolution | All behaviors are resolved first, outermost to innermost, then the handler. All of this happens before any behavior runs. | An activation failure never leaves a pipeline partly executed. | A short-circuiting behavior still pays for constructing the handler. | Yes (observable) |
| 14 | Cancellation | One check at entry, after the handler lookup. After that the token is only forwarded. | Skips work for requests that are already doomed, and configuration errors still surface regardless of the token. | Behaviors never see a send whose token was already canceled. | Yes |
| 15 | Failure channel | `SendAsync` reports every failure through its returned `Task` and never throws synchronously. | Every failure behaves the same way under `await`. | None of note. | Yes |
| 16 | Exception taxonomy | `ArbitratorConfigurationException : InvalidOperationException`, plus standard BCL exceptions. | Lets callers tell an Arbitrator misconfiguration apart from an `InvalidOperationException` thrown by a handler. | One more public type. | Yes |
| 17 | Notifications | Deferred to v0.2. | Keeps the v0.1 surface focused on requests. | Applications that need in-process fan-out must wait for v0.2 or write their own. | No |
| 18 | Packaging | A single `Arbitrator` package. | A separate abstractions package has no consumer yet, and type forwarding lets it be split out later without breaking anyone. | Assemblies that only define contracts also reference `Microsoft.Extensions.DependencyInjection.Abstractions`. | No (decide before publishing) |
| 19 | Trimming and AOT | Trimming annotations and AOT analyzers are enabled, with `RequiresDynamicCode` and `RequiresUnreferencedCode` on the entry points that use reflection. No AOT claim. | Honest about the reflection that remains. | Native AOT consumers get warnings. | No |
| 20 | License | Apache-2.0 (§9.6). | Explicit patent grant; compatible with every dependency. | Longer than MIT. | Settled |
| 21 | Test framework | TUnit on Microsoft.Testing.Platform. | Owner preference; source-generated discovery, parallel by default, async assertions. | Assertions are awaited, unlike xUnit's synchronous `Assert`. | Settled |

### Alternatives considered

- **A separate pipeline for no-response requests.** Rejected because it doubles the behavior surface, and open behaviors would have to be written twice.
- **Handlers that return `Unit`.** Rejected because handlers would have to write `return Unit.Value`, which is exactly the meaningless value the goals exclude.
- **A singleton sender that finds the scope through `AsyncLocal` or `IHttpContextAccessor`.** Rejected because it's implicit, ties the library to ASP.NET Core, and breaks in background work.
- **Resolving handlers by their `IRequestHandler<,>` service type.** Rejected because MS.DI silently returns the last registration, and proving there's only one would cost an enumeration on every dispatch.
- **Registering open behaviors as open generics in DI.** Rejected because container validation skips open-generic descriptors, which was confirmed against MS.DI. Their dependency errors would then appear only at dispatch.
- **Resolving the handler lazily, only when the innermost continuation runs.** Rejected because an activation failure could happen after outer behaviors had already started work, such as opening a transaction.
- **`ValueTask`.** Rejected (decision 2).

---

## 4. Public API proposal

The signatures below are the complete public surface of v0.1. Bodies appear only where the body is part of the contract. Every public type also gets XML documentation.

### 4.1 Contracts (namespace `Arbitrator`)

```csharp
namespace Arbitrator;

public readonly record struct Unit
{
    public static Unit Value => default;
}

public interface IRequest<TResponse>;

public interface IRequest : IRequest<Unit>;

public interface IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

public interface IRequestHandler<TRequest> : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest
{
    new Task HandleAsync(TRequest request, CancellationToken cancellationToken);

    async Task<Unit> IRequestHandler<TRequest, Unit>.HandleAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        Task handling = HandleAsync(request, cancellationToken)
            ?? throw DispatchErrors.NullHandlerTask(GetType(), typeof(TRequest));

        await handling.ConfigureAwait(false);
        return Unit.Value;
    }
}

public delegate Task<TResponse> RequestContinuation<TResponse>(CancellationToken cancellationToken);

public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(
        TRequest request,
        RequestContinuation<TResponse> next,
        CancellationToken cancellationToken);
}

public interface ISender
{
    Task<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);

    Task SendAsync(IRequest request, CancellationToken cancellationToken = default);
}

public sealed class ArbitratorConfigurationException : InvalidOperationException
{
    public ArbitratorConfigurationException(string message);
    public ArbitratorConfigurationException(string message, Exception? innerException);
}
```

Why the contracts look like this:

- **Invariance.** None of the interfaces declares variance. Contravariant handlers would suggest polymorphic dispatch, which Arbitrator deliberately doesn't do. A covariant `IRequest<out TResponse>` would let `SendAsync<object>(getCustomer)` compile against a handler that returns `CustomerDto`.
- **The `TRequest : IRequest<TResponse>` constraint on handlers and behaviors.** A handler or closed behavior whose response type doesn't match its request becomes a compile error. Open behaviors must restate the constraint, and the compiler enforces that (CS0314).
- **Bridging no-response handlers to `Unit`.** `IRequestHandler<TRequest>` hides the base `HandleAsync` with `new` and implements it explicitly as a default interface method. A class implementing `IRequestHandler<DeactivateCustomer>` writes only `Task HandleAsync(...)`. Arbitrator calls it through `IRequestHandler<DeactivateCustomer, Unit>`, and the bridge supplies `Unit.Value`. The bridge also reports a null task from the handler as the same `InvalidOperationException` that response handlers get (§7 row 17); `DispatchErrors` is internal to the library. A class may implement `IRequestHandler<TRequest, Unit>` directly instead, and Arbitrator treats that the same way.
- **Default for `cancellationToken`.** The token has a default value only on `ISender`, as a convenience for callers. Handlers and behaviors must accept it explicitly.
- **Why `Unit` is public.** Behaviors observe `Unit` as `TResponse`, and a closed behavior for a no-response request must name it, for example `IPipelineBehavior<DeactivateCustomer, Unit>`.

### 4.2 Registration (namespace `Microsoft.Extensions.DependencyInjection` for the entry point)

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public static class ArbitratorServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddArbitrator(Action<ArbitratorBuilder> configure);
    }
}
```

```csharp
namespace Arbitrator;

public sealed class ArbitratorBuilder
{
    internal ArbitratorBuilder(/* registry, services */);

    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public ArbitratorBuilder AddHandler<THandler>() where THandler : class;

    public ArbitratorBuilder AddBehavior<TBehavior>() where TBehavior : class;

    [RequiresDynamicCode(RegistrationReflection.DynamicCodeMessage)]
    [RequiresUnreferencedCode(RegistrationReflection.UnreferencedCodeMessage)]
    public ArbitratorBuilder AddOpenBehavior(Type openBehaviorType);
}
```

- `AddHandler<THandler>()` registers every closed `IRequestHandler<,>` contract that `THandler` implements.
- `AddBehavior<TBehavior>()` registers a closed behavior, which applies to every `IPipelineBehavior<TRequest, TResponse>` it implements.
- `AddOpenBehavior(typeof(X<,>))` registers an open-generic behavior.
- Each builder call either completes entirely or changes nothing: it validates first and only then mutates. The methods return the builder so calls can be chained.
- `THandler`, `TBehavior`, and `openBehaviorType` carry `[DynamicallyAccessedMembers(PublicConstructors | Interfaces)]` (§5.11).
- `AddArbitrator` lives in `Microsoft.Extensions.DependencyInjection` so that it's discoverable through the implicit usings of ASP.NET Core and Worker projects. This is a deliberate exception to the rule that namespaces match folders.

### 4.3 Call-site inference

Each row below was checked against the .NET 11 RC3 compiler using these contracts.

| Call site | Overload chosen | Static result type |
| --- | --- | --- |
| `sender.SendAsync(new GetCustomer(id))` where `GetCustomer : IRequest<CustomerDto?>` | Generic; `TResponse` = `CustomerDto?` | `Task<CustomerDto?>` |
| `sender.SendAsync(new DeactivateCustomer(id, reason))` where `DeactivateCustomer : IRequest` | `SendAsync(IRequest)`. `IRequest` is a better conversion target than `IRequest<Unit>` | `Task` |
| `IRequest<CustomerDto?> request = ...; sender.SendAsync(request)` | Generic | `Task<CustomerDto?>` |
| `IRequest command = ...; sender.SendAsync(command)` | `SendAsync(IRequest)` | `Task` |
| `IRequest<Unit> request = ...; sender.SendAsync(request)` | Generic; `TResponse` = `Unit` | `Task<Unit>` (legal, but not the documented style) |
| `readonly record struct Ping : IRequest<int>` | Generic; the request is boxed at the call | `Task<int>` |
| `sender.SendAsync<object>(new GetCustomer(id))` | Compile error, because `IRequest<>` is invariant | n/a |
| A type that implements both `IRequest<A>` and `IRequest<B>` | Compile error CS0411 unless `TResponse` is given explicitly. Registering its handler fails anyway (§8) | n/a |
| `object request = ...; sender.SendAsync(request)` | Compile error. There is no object-based dispatch | n/a |

In every case, the static type at the call site is used only to choose `TResponse`. The handler and pipeline are always selected by `request.GetType()` (§5.7).

### 4.4 Examples

**Requests:**

```csharp
public sealed record CustomerDto(Guid Id, string Name, bool IsActive);

public sealed record GetCustomer(Guid Id) : IRequest<CustomerDto?>;

public sealed record DeactivateCustomer(Guid Id, string Reason) : IRequest, IValidatable, ITransactional
{
    public IReadOnlyList<string> Validate() =>
        string.IsNullOrWhiteSpace(Reason) ? ["Reason is required."] : [];
}
```

`IValidatable` and `ITransactional` are interfaces the application defines, not Arbitrator.

**Handler with a scoped dependency, returning a DTO:**

```csharp
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

**Handler for a request with no response:**

```csharp
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

**Logging behavior (open generic, applies to every request):**

```csharp
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestContinuation<TResponse> next,
        CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();
        RequestLog.Handling(logger, typeof(TRequest).Name);

        try
        {
            return await next(cancellationToken);
        }
        finally
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
            RequestLog.Handled(logger, typeof(TRequest).Name, elapsed);
        }
    }
}

internal static partial class RequestLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Handling {RequestType}")]
    public static partial void Handling(ILogger logger, string requestType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestType} in {Elapsed}")]
    public static partial void Handled(ILogger logger, string requestType, TimeSpan elapsed);
}
```

**Validation behavior (constrained open generic, applies only to `IValidatable` requests):**

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

**Transaction behavior (constrained, scoped dependency, unwinds on exception):**

```csharp
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ITransactional
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestContinuation<TResponse> next,
        CancellationToken cancellationToken)
    {
        await using ITransaction transaction = await unitOfWork.BeginAsync(cancellationToken);
        TResponse response = await next(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }
}
```

**DI registration:**

```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<ICustomerRepository, SqlCustomerRepository>();
builder.Services.AddScoped<IUnitOfWork, SqlUnitOfWork>();

builder.Services.AddArbitrator(arbitrator => arbitrator
    .AddOpenBehavior(typeof(LoggingBehavior<,>))
    .AddOpenBehavior(typeof(ValidationBehavior<,>))
    .AddOpenBehavior(typeof(TransactionBehavior<,>))
    .AddHandler<GetCustomerHandler>()
    .AddHandler<DeactivateCustomerHandler>());
```

The resulting pipelines are:

- `GetCustomer`: Logging → handler.
- `DeactivateCustomer`: Logging → Validation → Transaction → handler.

**Dispatch through `ISender` in request-scoped code:**

```csharp
app.MapGet("/customers/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
{
    CustomerDto? customer = await sender.SendAsync(new GetCustomer(id), cancellationToken);
    return customer is null ? Results.NotFound() : Results.Ok(customer);
});

app.MapPost("/customers/{id:guid}/deactivate",
    async (Guid id, DeactivationForm form, ISender sender, CancellationToken cancellationToken) =>
    {
        await sender.SendAsync(new DeactivateCustomer(id, form.Reason), cancellationToken);
        return Results.NoContent();
    });
```

**Dispatch from background work, which owns its scope:**

```csharp
public sealed class TrialExpiryWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.SendAsync(new ExpireTrials(), stoppingToken);
    }
}
```

---

## 5. Execution model

### 5.1 Public contract vs. internal implementation

**Public contract (stable, normative).** This consists of:

- The public types in §4.
- The ordering rule (§5.6).
- Behavior applicability (§5.3).
- Scope and lifetime semantics (§6).
- The cancellation and exception rules (§7).
- The exception types and the moments when they're thrown (§8).

**Internal implementation (proposed, non-normative).** The registry, dispatch table, invoker types, the private service key, step objects, and the timing of descriptor emission are internal. They may change in any release, provided the contract still holds. The tests in §10 exercise only the public contract.

### 5.2 Registration (internal)

1. **The first `AddArbitrator` call on a service collection** adds three descriptors:
   - `ISender` → internal `Sender`, scoped.
   - An internal `ArbitratorRegistry`, as a singleton instance.
   - An internal `DispatchTable`, as a singleton built by a factory from the registry.
2. **Later calls** find the existing registry by scanning the collection for its descriptor, and add to it. They add no further core descriptors.
3. **`AddHandler<THandler>()`**:
   - Validates `THandler` (§8.2).
   - Records one handler contract (`TRequest`, `TResponse`, `THandler`) for each closed `IRequestHandler<,>` interface found on it.
   - Adds one keyed transient descriptor: service type `THandler`, implementation type `THandler`, with the private key.
   - For every open behavior already registered that applies to a new contract, adds a keyed transient descriptor for the closed behavior type.
4. **`AddBehavior<TBehavior>()`** validates the type, records it at the next registration position, and adds one keyed transient descriptor for `TBehavior`.
5. **`AddOpenBehavior(type)`** validates the shape (§8.2) and records the type at the next registration position. For every handler contract already registered that it applies to, it adds a keyed transient descriptor for the closed instantiation.

Because emission is incremental (steps 3 and 5), the relative order of handler and behavior calls never matters. Arbitrator never builds a service provider during registration.

**Applicability of a behavior to a handler contract (`TRequest`, `TResponse`):**

- **A closed behavior** applies when it implements `IPipelineBehavior<TRequest, TResponse>` exactly.
- **An open behavior** applies when closing it over (`TRequest`, `TResponse`) passes the runtime's generic-constraint check. Internally this means `MakeGenericType` succeeds; an `ArgumentException` caused by a constraint violation means the behavior doesn't apply.
  - The runtime enforces `class`, `struct`, `new()`, base-class, and interface constraints, including constraints that refer to the other type parameter.
  - The runtime doesn't enforce `notnull` or nullable reference annotations, and `unmanaged` is enforced only as `struct`. Applicability ignores all of these.
  - Arbitrator computes applicability once per (behavior, contract) pair, during registration.

### 5.3 Building the dispatch table (internal)

The `DispatchTable` singleton is created the first time a provider resolves `ISender`. Creation proceeds as follows:

1. Marks the registry as frozen. Any later builder call throws `ArbitratorConfigurationException`.
2. Checks that every closed behavior applies to at least one registered handler contract. Otherwise it throws `ArbitratorConfigurationException`, and because the singleton factory threw, every attempt to resolve `ISender` fails the same way.
3. For each handler contract, creates a `RequestInvoker<TRequest, TResponse>` using `MakeGenericType`. The invoker holds:
   - The handler's implementation type.
   - The applicable closed behavior types, as an array in registration order.
4. Stores the invokers in a `FrozenDictionary<Type, RequestInvoker>` keyed by request type.

5. Checks, through the provider's `IServiceProviderIsKeyedService`, that the provider can resolve every handler and behavior type the invokers refer to. A type is missing when it was registered after this provider was built, because MS.DI does not make a collection read-only when it builds a provider. The table then throws `ArbitratorConfigurationException` naming the missing types, instead of letting dispatch fail later with a DI error. Providers that don't offer `IServiceProviderIsKeyedService` skip this check.

### 5.4 Dispatch steps (contract, observable order)

`SendAsync` is an `async` method, so each failure below arrives as a faulted or canceled `Task`.

1. `request` is null → `ArgumentNullException`. Nothing is resolved.
2. The table has no entry for `request.GetType()` → `ArbitratorConfigurationException` (missing handler). Nothing is resolved.
3. `cancellationToken.ThrowIfCancellationRequested()` → `OperationCanceledException` carrying the caller's token. Nothing is resolved.
4. Resolve every behavior, outermost first, then the handler, from the scope's `IServiceProvider` using the private key. DI exceptions propagate unwrapped.
5. Build the continuation chain from the inside out and invoke the outermost component with the caller's token.
6. Await the result with `ConfigureAwait(false)` and return it. `SendAsync(IRequest)` discards the `Unit`.

### 5.5 Sequence diagram

```
Caller          Sender(scoped)        DispatchTable      Scope (DI)        B1 (outer)        B2             Handler
  |  SendAsync(r, ct) |                     |                 |                 |               |                |
  |------------------>| null check          |                 |                 |               |                |
  |                   |--Find(r.GetType())->|                 |                 |               |                |
  |                   |<----- invoker ------|                 |                 |               |                |
  |                   | ct.ThrowIfCancellationRequested()     |                 |               |                |
  |                   |--resolve B1, B2, H (keyed) ---------->|                 |               |                |
  |                   |<---------------- instances -----------|                 |               |                |
  |                   |--B1.HandleAsync(r, next1, ct) -------------------------->|               |                |
  |                   |                     |                 |                 |--next1(ct)--->|                |
  |                   |                     |                 |                 |               |--next2(ct)---->|
  |                   |                     |                 |                 |               |<--Task<R>------|
  |                   |                     |                 |                 |<--Task<R>-----|                |
  |                   |<------------------------- Task<R> ----------------------|               |                |
  |<----- Task<R> ----|  result or exception passes through unchanged           |               |                |
```

`next1` calls `B2.HandleAsync(r, next2, token)`, and `next2` calls `Handler.HandleAsync(r, token)`. Each continuation passes on the token it was given.

### 5.6 Nested ordering example

Suppose the registrations are made in this order, across two `AddArbitrator` calls:

1. `AddOpenBehavior(typeof(LoggingBehavior<,>))`. First call.
2. `AddBehavior<AuditDeactivationBehavior>()`. First call. This behavior implements `IPipelineBehavior<DeactivateCustomer, Unit>`.
3. `AddOpenBehavior(typeof(ValidationBehavior<,>))`. Second call. It's constrained to `IValidatable`.

Each behavior writes a line before and after `next`. Sending `DeactivateCustomer` prints:

```
Logging.before
  Audit.before
    Validation.before
      DeactivateCustomerHandler
    Validation.after
  Audit.after
Logging.after
```

`GetCustomer` doesn't implement `IValidatable` and isn't audited, so sending it prints `Logging.before`, then `GetCustomerHandler`, then `Logging.after`.

If the handler throws, every `after` line that sits in a `finally` still runs, from the innermost behavior outward. Lines that follow `await next(...)` outside a `finally` don't run. The caller receives the handler's exception instance.

### 5.7 Connecting the runtime request type to typed dispatch (internal)

The internal types are:

- `RequestInvoker<TResponse>`: abstract, with `InvokeAsync(IRequest<TResponse>, IServiceProvider, CancellationToken)`.
- `RequestInvoker<TRequest, TResponse>`: sealed, derives from `RequestInvoker<TResponse>`, and is constructed once per handler contract.

On each send:

1. `Sender.SendAsync<TResponse>` looks up `request.GetType()` and casts the entry to `RequestInvoker<TResponse>`. The cast can't fail: registration guarantees that a request type implements exactly one `IRequest<>` and that the contract's `TResponse` is that interface's argument. Since `IRequest<>` is invariant, the call site's `TResponse` must be identical. `SendAsync(IRequest)` uses the same path with `TResponse` = `Unit`.
2. Inside the typed invoker, `(TRequest)request` is the only cast. It unboxes struct requests. Everything after it is statically typed generic code.

So the interface-typed reference never decides anything: the runtime type selects the invoker, and the invoker restores `TRequest`. A dispatch involves one dictionary lookup, one virtual call, and two casts. There's no reflection and no `MethodInfo.Invoke` per dispatch, so exceptions are never wrapped in `TargetInvocationException`.

**Per-dispatch composition.** Each behavior is wrapped in a small step object that holds:

- The behavior instance.
- The typed request.
- The next continuation.
- An "invoked" flag, set with `Interlocked.Exchange`.

Its `Invoke(CancellationToken)` method becomes the `RequestContinuation<TResponse>` that the behavior receives. The step object also turns a null `Task` returned by its behavior into an `InvalidOperationException`. The handler is wrapped the same way, as the innermost continuation. With zero behaviors, Arbitrator calls the handler directly and allocates no step objects.

### 5.8 Caching

| Data | Where | Built | Mutability |
| --- | --- | --- | --- |
| Ordered handler and behavior registrations, plus the set of descriptors already emitted | The `ArbitratorRegistry` instance, held as a singleton descriptor in the `IServiceCollection` | On each builder call | Changes until frozen, then rejects further changes |
| Request type → `RequestInvoker` map | The `DispatchTable` singleton, one per root provider | On the first `ISender` resolution in that provider | Immutable (`FrozenDictionary`) |
| Handler implementation type and ordered closed behavior types | Inside each `RequestInvoker` | Same as above | Immutable |

**Never cached, anywhere:**

- Handler or behavior instances.
- Any `IServiceProvider` or scope, beyond the scope's own `Sender` holding the provider of the scope it belongs to.
- Requests, responses, cancellation tokens, continuation delegates, step objects, or exceptions.

Arbitrator has no static fields that hold mutable state. The only static is the immutable private service-key sentinel. When two providers are built from one collection, each builds its own identical table from the shared frozen registry.

### 5.9 Concurrency

- The dispatch table and invokers are immutable after construction. MS.DI runs a singleton factory only once, so concurrent first resolutions are safe.
- All per-dispatch state lives on the call path: the resolved instances, step objects, and guard flags. No two sends share request-specific state.
- Handlers and behaviors are transient, so concurrent sends within one scope get distinct instances.
- Arbitrator doesn't make **scoped dependencies** thread-safe. Two concurrent sends in one scope share that scope's `DbContext`, for example, and concurrent use of it is the caller's responsibility.

### 5.10 Nested sends

A handler or behavior may inject `ISender` and send another request. Here's what that does and doesn't give you:

- **Same scope.** The nested send runs in the same scope, so it shares scoped services and the same unit of work.
- **Full pipeline.** The nested send goes through its own complete pipeline, so outer behaviors such as logging see it as a separate send.
- **What isn't guaranteed:**
  - No recursion or cycle detection. Unbounded recursion ends in stack or memory exhaustion.
  - No ambient "parent request" context.
  - No coordination of transactions or ordering beyond what the code itself awaits.
  - No protection when nested sends run concurrently against shared scoped services.

### 5.11 Trimming and Native AOT (considerations, not promises)

- **Reflection still in use.** Arbitrator calls `GetInterfaces` on handler and behavior types and `MakeGenericType` to close invokers and open behaviors over request and response types. Some of those types are value types, such as `Unit` and `int`. Explicit registration names the types, but it doesn't remove this runtime generic instantiation. Explicit registration therefore isn't evidence of AOT compatibility.
- **Library project settings.** `IsTrimmable` and `IsAotCompatible` are enabled so the analyzers run. Each warning is resolved one of two ways:
  - By an annotation such as `[DynamicallyAccessedMembers(PublicConstructors | Interfaces)]` on handler and behavior type parameters.
  - By `RequiresDynamicCode`, or `RequiresUnreferencedCode` where the analyzer requires it, on the public entry point. Blanket suppressions aren't used. Internal code that's reachable only through an annotated entry point may carry a targeted suppression whose justification names that entry point.
- **Outcome for v0.1.** `AddHandler` and `AddOpenBehavior` carry both `RequiresDynamicCode` and `RequiresUnreferencedCode`: handler registration inspects the interfaces of request types, which trimming can't analyze, and both close generic types at runtime. `AddBehavior` carries neither. The library builds with no trimming or AOT analyzer warnings and no suppressions. Native AOT publishing produces warnings and isn't supported.
- **Possible future AOT path.** A source generator could emit closed registrations and invokers (§13).

---

## 6. Dependency injection and lifecycle semantics

### 6.1 Lifetimes

| Service | Lifetime | Registration |
| --- | --- | --- |
| `ISender` (internal `Sender`) | Scoped | Unkeyed. The application may inject it anywhere a scoped service is valid |
| Dispatch table and registry | Singleton | Internal types only |
| Handlers | Transient | Keyed with the private key, registered as their implementation type, one descriptor per handler type |
| Closed behaviors | Transient | Keyed, registered as their implementation type |
| Open behaviors | Transient | Keyed. One closed descriptor per applicable registered request |

A handler that implements several contracts has one descriptor and still gets a new instance for every send. If the application also registers a handler type itself, for example `AddScoped<GetCustomerHandler>()`, that registration is separate and Arbitrator never uses it.

### 6.2 Scopes

- **Which scope dispatch uses.** Dispatch uses exactly the scope that resolved the `ISender`, because the scoped `Sender` captures that scope's `IServiceProvider`. Arbitrator never creates a scope. Code without one, such as hosted services, timers, or message consumers, must create a scope with `IServiceScopeFactory` and resolve `ISender` from it (§4.4).
- **Resolving `ISender` from the root provider:**
  - With `ValidateScopes` enabled, MS.DI throws `InvalidOperationException` ("Cannot resolve scoped service 'Arbitrator.ISender' from root provider."). The host builders enable it in the Development environment.
  - With it disabled, resolution succeeds and everything is resolved from the root for the application's lifetime. Scoped dependencies effectively become singletons that are shared across concurrent work, and transient disposable handlers and behaviors pile up in the root until shutdown, which is a memory leak.
- **Capturing `ISender` in a singleton** has the same effect. Container validation (`ValidateScopes` with `ValidateOnBuild`) reports it at build time, because a singleton depends on a scoped service.
- **No ambient scope.** Arbitrator can't infer an ambient scope. It doesn't consult `AsyncLocal`, `IHttpContextAccessor`, or any similar mechanism.

### 6.3 Disposal

The DI container owns disposal, and Arbitrator never calls `Dispose` or `DisposeAsync`:

- Transient disposable handlers and behaviors are disposed when the scope that resolved them is disposed, not when `SendAsync` completes.
- A long-lived scope that dispatches many requests keeps every disposable transient alive until the scope ends. The guidance is one scope per unit of work.
- If a service implements only `IAsyncDisposable`, its scope must be disposed asynchronously. This is MS.DI's rule.

### 6.4 Repeated `AddArbitrator` calls and duplicates

**Repeated calls**

- Repeated calls on the same collection are supported and add to one registry.
- Behavior order is the global order of registration calls across all `AddArbitrator` calls.
- Separate `IServiceCollection` instances have independent registries.

**Duplicates.** Each of the following is an `ArbitratorConfigurationException`, thrown from the offending builder call:

| Situation | Result |
| --- | --- |
| A second handler type for a request type that already has one | Error |
| The same handler type registered twice | Error |
| The same behavior type registered twice, open or closed | Error |
| A closed behavior type that equals the instantiation of a registered open behavior for some registered request | Error, because the behavior would run twice |

**Other cases**

- **Changes after freezing.** Builder calls made after a provider has attempted to build the dispatch table throw `ArbitratorConfigurationException`. This holds even when the attempt failed, so the registrations a provider was checked against can't change underneath it.
- **Registrations after a provider was built.** Registrations made after `BuildServiceProvider`, but before that provider first resolves `ISender`, are accepted. That provider can't resolve them, so its first `ISender` resolution throws `ArbitratorConfigurationException` naming them (§5.3).
- **Read-only collections.** Builder calls on a read-only collection (for example after `HostApplicationBuilder.Build()`) throw `ArbitratorConfigurationException` before anything changes.
- **Copied collections.** A collection whose descriptors were copied from another collection initially shares the original's registry. On its first `AddArbitrator` call it replaces that registry with an independent, unfrozen copy, so later registrations on either collection don't affect the other.
- **Handlers registered without Arbitrator.** `IRequestHandler<,>` and `IPipelineBehavior<,>` services registered directly on the collection are ignored. Arbitrator neither resolves nor validates them.

### 6.5 Validation boundaries

| Stage | What is detected | What can't be detected here |
| --- | --- | --- |
| **Registration** (each builder call, atomic) | Null arguments. A read-only collection. Invalid handler or behavior shapes, including value-type, generic-parameter, and partially closed behavior types. Abstract request types, and request types with more than one `IRequest<>`. All duplicate cases. Calls made after freezing | Constructor dependencies. Whether the application has requests without handlers |
| **Container validation** (`ValidateOnBuild`, `ValidateScopes`) | Unresolvable constructor dependencies of every handler and every behavior instantiation that applies to a registered request, because all of them are closed keyed descriptors. Singletons that depend on `ISender`. Lifetime violations inside application services | Exceptions thrown by constructors. Arbitrator's own cross-registration rules |
| **First `ISender` resolution** in a provider | Closed behaviors that match no registered handler contract. Handlers and behaviors registered after the provider was built | Requests that are never registered |
| **Dispatch** | Null request. A request type with no handler, including derived types of handled requests. Constructor exceptions. A continuation invoked twice. Null `Task` results | n/a |
| **Never** | Arbitrator can't prove that every request type in the application has a handler. It doesn't check that handlers honor cancellation, or that a non-nullable `TResponse` is actually non-null | |

**Recommended application-side test:** build the real container with `ValidateOnBuild` and `ValidateScopes` enabled, create a scope, and resolve `ISender`. Together these cover every stage before dispatch.

---

## 7. Behavioral contract

"Arbitrator" in the right-hand column means the library itself enforces the rule. "Forwarded" means Arbitrator only passes values or exceptions through.

| # | Scenario | Contract | Enforced by |
| --- | --- | --- | --- |
| 1 | Request with a response | The handler runs once and receives the caller's request instance and token. `SendAsync` returns the instance that the outermost component returned | Arbitrator |
| 2 | Request without a response | `SendAsync(IRequest)` completes when the pipeline completes. Behaviors see `TResponse` = `Unit`. Neither the handler nor the caller touches `Unit` | Arbitrator |
| 3 | Null request | The task faults with `ArgumentNullException` (parameter `request`). Nothing is resolved | Arbitrator |
| 4 | Missing handler | The task faults with `ArbitratorConfigurationException`. Nothing is resolved and no behavior runs. Detected before the cancellation check | Arbitrator |
| 5 | Request type derived from a handled type | Treated as a missing handler. The message names the registered base type | Arbitrator |
| 6 | Duplicate handlers | Can't reach dispatch: rejected at registration (§6.4) | Arbitrator |
| 7 | Request held through an interface-typed reference | Dispatched by runtime type. Behaves exactly like a concrete-typed reference | Arbitrator |
| 8 | Struct request | Supported. Boxed when passed to `SendAsync`. Each behavior and the handler receive a copy of the value | Arbitrator |
| 9 | No matching behaviors | The handler is invoked directly | Arbitrator |
| 10 | Multiple matching behaviors | They run in registration order, first registered outermost, open and closed alike. Constrained behaviors that don't apply are left out | Arbitrator |
| 11 | Short-circuit | A behavior returns without calling `next`. Inner behaviors and the handler aren't invoked, although they were already constructed. The behavior's value becomes the result, and no-response requests return `Unit.Value` | Arbitrator |
| 12 | A behavior replaces the response | Allowed. The outermost component's value is the result | Forwarded |
| 13 | The handler throws | The same exception instance reaches each enclosing behavior's `await` and then the caller. Its stack trace starts in the handler. Arbitrator doesn't catch it | Forwarded |
| 14 | A behavior throws before calling `next` | Inner components aren't invoked. The same exception instance propagates | Forwarded |
| 15 | A behavior catches an inner exception | It may rethrow, translate, or swallow the exception and return a response. Arbitrator imposes nothing | Behavior |
| 16 | Activation failure (missing dependency or a constructor that throws) | MS.DI's exception propagates unwrapped. No behavior has run yet | Forwarded |
| 17 | A handler or behavior returns a null `Task` | `InvalidOperationException` naming the component and the request type | Arbitrator |
| 18 | Token already canceled | After the handler lookup, the task is canceled with an `OperationCanceledException` that carries the caller's token. Nothing is resolved or run | **Checked by Arbitrator, once** |
| 19 | Cancellation during execution | Forwarded only. Arbitrator neither observes the token nor checks it between components. If the code throws `OperationCanceledException`, it propagates unchanged and the task ends up canceled. If the code finishes regardless, its result is returned. Arbitrator doesn't promise prompt cancellation | Forwarded |
| 20 | A behavior substitutes the token | Allowed. Everything inside receives the substituted token, while outer components keep their own. The behavior owns the token source and must keep it alive until `next` completes. Arbitrator doesn't require the token to be linked to the original, so passing `CancellationToken.None` (dropping caller cancellation) is allowed and the behavior author's responsibility | Behavior |
| 21 | `next` invoked a second time, sequentially or concurrently | The second invocation throws `InvalidOperationException` synchronously from `next`. Inner components ran exactly once | Arbitrator |
| 22 | `next` invoked after the behavior's own task has completed | Unsupported and not guarded. Scoped services may already be disposed | Not guarded |
| 23 | Concurrent sends in different scopes | Fully isolated | Arbitrator plus DI |
| 24 | Concurrent sends in one scope | Arbitrator's state is safe, and handlers and behaviors are distinct instances. Shared scoped services aren't protected | Arbitrator plus caller |
| 25 | Nested send | Permitted. It runs in the same scope through its own full pipeline. No recursion detection (§5.10) | Arbitrator |
| 26 | `ISender` resolved from the root provider | DI throws if scope validation is enabled. Otherwise everything resolves from the root (§6.2) | DI |
| 27 | A handler returns null for a non-nullable `TResponse` | Not checked. The value is returned as is | Not checked |
| 28 | One class implements several handler contracts | Each contract dispatches normally, with a new instance per send | Arbitrator |
| 29 | `SendAsync` called without being awaited | Never throws synchronously. Every failure in this table appears through the returned `Task` | Arbitrator |

**Example of token substitution (row 20):**

```csharp
public sealed class TimeoutBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IHasTimeout
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestContinuation<TResponse> next,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);
        return await next(timeout.Token);
    }
}
```

---

## 8. Diagnostics and exceptions

### 8.1 Exception taxonomy

| Exception | Thrown by | When |
| --- | --- | --- |
| `ArgumentNullException` | Arbitrator | A null `configure`, `openBehaviorType`, or `request` |
| `ArbitratorConfigurationException` (derives from `InvalidOperationException`) | Arbitrator | Every registration error (§8.2), a closed behavior with no handler when the table is built, a builder call after freezing, and a missing handler at dispatch |
| `InvalidOperationException` | Arbitrator | Misuse of the contract by user code at runtime: a continuation invoked twice, or a null `Task` result. These are bugs in a behavior or handler, not configuration problems |
| `OperationCanceledException` | Arbitrator, from the entry check, or user code | The caller's token is already canceled at entry. Any other cancellation comes from user code |
| `InvalidOperationException` from MS.DI | DI | Unresolvable dependencies, or resolving a scoped service from the root |
| Anything else | User code | Propagates unwrapped, as the same instance |

`ArbitratorConfigurationException` is the only custom type. It exists so that hosts and tests can tell "Arbitrator is misconfigured" apart from an `InvalidOperationException` thrown by a handler. Arbitrator adds no custom types for cancellation, handler failures, or activation failures, because the existing exceptions already carry the needed information. A `RequestValidationException`, if wanted, belongs to the application, as in the example in §4.4.

### 8.2 Messages

Type names in messages use C# syntax and include namespaces, for example `IRequestHandler<Contoso.Customers.GetCustomer, Contoso.Customers.CustomerDto>`. No-response contracts appear as `IRequestHandler<Contoso.Customers.DeactivateCustomer>`. The wording below is representative. Tests assert on the exception type and the type names a message contains, not on exact text.

| Failure | Stage | Representative message |
| --- | --- | --- |
| Missing handler | Dispatch | `No handler is registered for request type 'Contoso.Customers.GetCustomer'. Register a class implementing IRequestHandler<Contoso.Customers.GetCustomer, Contoso.Customers.CustomerDto?> with AddHandler<THandler>(). Handlers are matched to the exact runtime type of the request.` |
| Missing handler, base type registered | Dispatch | The missing-handler message, followed by: `A handler is registered for base type 'Contoso.Customers.GetCustomer'; handlers for base types are not used for 'Contoso.Customers.GetVipCustomer'.` |
| Second handler for a request | Registration | `Request type 'Contoso.Customers.GetCustomer' already has handler 'Contoso.Customers.GetCustomerHandler'; cannot also register 'Contoso.Legacy.LegacyGetCustomerHandler'. Each request type has exactly one handler.` |
| Handler type registered twice | Registration | `Handler type 'Contoso.Customers.GetCustomerHandler' is already registered. Each handler type may be registered once across all AddArbitrator calls.` |
| Handler with no contract | Registration | `Type 'Contoso.Customers.CustomerService' cannot be registered as a handler: it implements neither IRequestHandler<TRequest, TResponse> nor IRequestHandler<TRequest>.` |
| Abstract handler | Registration | `Handler type 'Contoso.Customers.HandlerBase' is abstract; register a concrete class.` |
| Request with several request contracts | Registration | `Request type 'Contoso.Customers.Ambiguous' implements more than one request contract (IRequest<A>, IRequest<B>). A request type must implement exactly one of IRequest or IRequest<TResponse>.` |
| Handler for an abstract or interface request type | Registration | `Handler 'Contoso.Customers.QueryHandler' handles 'Contoso.Customers.QueryBase', which is abstract or an interface. Requests are dispatched by their exact runtime type, so this handler could never run; register a handler for each concrete request type.` |
| Invalid open behavior | Registration | `Open behavior 'Contoso.Pipeline.AuditBehavior<T>' must be a generic type definition with exactly two type parameters that implements IPipelineBehavior<TRequest, TResponse> using those parameters in order.` |
| Closed type passed to `AddOpenBehavior` | Registration | `'Contoso.Pipeline.LoggingBehavior<Contoso.Customers.GetCustomer, Contoso.Customers.CustomerDto?>' is a closed type. Register it with AddBehavior<TBehavior>() or pass the generic type definition 'Contoso.Pipeline.LoggingBehavior<,>'.` |
| Generic parameter or partially closed type passed to `AddOpenBehavior` | Registration | `'TRequest' is a generic type parameter, not a behavior type. Pass a generic type definition, such as typeof(LoggingBehavior<,>).` or `'Contoso.Pipeline.LoggingBehavior<Contoso.Customers.GetCustomer, TResponse>' is partially closed. Pass the generic type definition 'Contoso.Pipeline.LoggingBehavior<,>'.` |
| Value-type open behavior | Registration | `Behavior type 'Contoso.Pipeline.TimingBehavior<,>' is a value type; behaviors must be classes.` |
| Read-only service collection | Registration | `The service collection is read-only, typically because the host has been built. Complete all AddArbitrator calls before building the host.` |
| Invalid closed behavior | Registration | `Type 'Contoso.Pipeline.Auditor' cannot be registered as a behavior: it does not implement IPipelineBehavior<TRequest, TResponse>.` (The same message is used for abstract types and interfaces, with the reason adjusted.) |
| Duplicate behavior | Registration | `Behavior 'Contoso.Pipeline.LoggingBehavior<,>' is already registered at position 1. Each behavior may be registered once; its registration position defines its place in the pipeline.` |
| Behavior would run twice | Registration | `Behavior 'Contoso.Pipeline.LoggingBehavior<Contoso.Customers.GetCustomer, Contoso.Customers.CustomerDto?>' would run twice for request 'Contoso.Customers.GetCustomer': registered as a closed behavior at position 3 and produced by open behavior 'Contoso.Pipeline.LoggingBehavior<,>' at position 1.` |
| Closed behavior with no handler | First `ISender` resolution | `Behavior 'Contoso.Customers.AuditDeactivationBehavior' implements IPipelineBehavior<Contoso.Customers.DeactivateCustomer, Arbitrator.Unit>, but no handler is registered for 'Contoso.Customers.DeactivateCustomer'.` |
| Change after freezing | Registration | `Arbitrator registrations cannot change once a service provider has attempted to resolve ISender. Complete all AddArbitrator calls before building the service provider.` |
| Registered after the provider was built | First `ISender` resolution | `These handlers and behaviors were registered after this service provider was built, so it cannot resolve them: 'Contoso.Customers.LateHandler'. Complete all AddArbitrator calls before building the service provider.` |
| Continuation invoked twice | Dispatch | `Behavior 'Contoso.Pipeline.RetryBehavior<Contoso.Customers.GetCustomer, Contoso.Customers.CustomerDto?>' invoked its continuation more than once while handling 'Contoso.Customers.GetCustomer'. A continuation may be invoked at most once.` |
| Null `Task` | Dispatch | `Handler 'Contoso.Customers.GetCustomerHandler' returned a null Task while handling 'Contoso.Customers.GetCustomer'.` |
| DI activation failure | Container validation or dispatch | MS.DI's own message, unwrapped, for example `Unable to resolve service for type 'Contoso.Customers.ICustomerRepository' while attempting to activate 'Contoso.Customers.GetCustomerHandler'.` |
| Cancellation | Entry or user code | The standard `OperationCanceledException` message. Its `CancellationToken` is the caller's token when the entry check throws |
| Exceptions from handlers and behaviors | Dispatch | Unchanged. Arbitrator adds no message |

### 8.3 Security considerations

- **No request contents in messages.** Arbitrator's messages contain type names and registration positions only. Arbitrator never calls `ToString()` on requests or responses, and never includes their values. Record types print every property, which could include personal or secret data.
- **Messages are for developers.** Messages name the application's types and namespaces. Applications must not return Arbitrator's exception messages to clients; doing so would disclose internal structure, though no data or access.
- **No logging, metrics, or tracing.** Arbitrator emits none of its own. Observability belongs in application behaviors, which choose what's safe to record. The logging example records only the request type name.
- **No string-to-type resolution.** Arbitrator has no object-based dispatch and never resolves a type from a string. Untrusted input can't choose a handler type. Dispatch only reaches handler types that the application registered explicitly.
- **Isolated service key.** The private key keeps Arbitrator's descriptors separate from application registrations. Arbitrator never overrides or removes descriptors that the application registered.
- **Recursion is the application's responsibility.** Unbounded nested sends can exhaust the stack or memory, and Arbitrator doesn't detect cycles (§5.10). Arbitrator adds no other resource-exhaustion surface, since it creates no threads, no timers, and no caches that grow with traffic.

---

## 9. Package and project structure

### 9.1 Package decision

v0.1 ships **one package, `Arbitrator`**, containing the contracts, the dispatcher, and the MS.DI integration. A separate `Arbitrator.Abstractions` package isn't justified yet:

- The library has no package dependencies at all. The DI abstractions it uses ship in the .NET 11 shared framework.
- No second container integration exists.
- The whole library is small.

The contracts can move later to a split-out package that the main package depends on, with `[TypeForwardedTo]` keeping existing binaries working. §13 lists what would justify that.

### 9.2 Solution layout after v0.1

```
Arbitrator/
  Arbitrator.slnx                      solution: src, tests (benchmarks added in M9)
  global.json                          SDK pin (rc.3 until GA, then 11.0.100)
  Directory.Build.props                shared build settings (§9.3)
  Directory.Packages.props             central package versions
  .editorconfig                        code style
  DESIGN.md                            this document
  README.md                            quick start, contract summary, link to DESIGN.md
  LICENSE                              Apache-2.0 (§9.6)
  src/Arbitrator/
    Arbitrator.csproj
    Unit.cs
    IRequest.cs
    IRequest{TResponse}.cs
    IRequestHandler{TRequest}.cs
    IRequestHandler{TRequest,TResponse}.cs
    IPipelineBehavior{TRequest,TResponse}.cs
    RequestContinuation{TResponse}.cs
    ISender.cs
    ArbitratorBuilder.cs
    ArbitratorConfigurationException.cs
    ArbitratorServiceCollectionExtensions.cs   (namespace Microsoft.Extensions.DependencyInjection)
    Registration/                      internal: ArbitratorRegistry, RegisteredPipelines, RegisteredBehaviors,
                                       PipelinePlan, HandlerContract, the BehaviorRegistration hierarchy,
                                       type inspectors, ComponentDescriptors, RegistrationErrors
    Dispatch/                          internal: Sender, DispatchTable, RequestInvoker types, guarded
                                       continuations, ComponentTasks, ArbitratorServiceKey, DispatchErrors
    Diagnostics/                       internal: TypeNameFormatter, ContractNames
  tests/Arbitrator.Tests/
    Arbitrator.Tests.csproj
    Dispatch/  NoResponse/  Pipeline/  Registration/  ContainerValidation/
    Cancellation/  Exceptions/  Scopes/  Disposal/  Concurrency/
    Examples/                          the §4.4 examples, compiled and exercised
    Support/                           Journal and test doubles
  benchmarks/Arbitrator.Benchmarks/    optional (M9), never packed
```

- **Namespaces.** Public types use `Arbitrator`. Internal types use `Arbitrator.Registration`, `Arbitrator.Dispatch`, and `Arbitrator.Diagnostics`, matching their folders.
- **Exception to the folder rule.** The single exception is the `AddArbitrator` extension class (§4.2).
- **No `InternalsVisibleTo`.** Tests use only the public API.

### 9.3 Build settings

These are shared through `Directory.Build.props`:

- **Language and framework:**
  - `TargetFramework` `net11.0`, with `LangVersion` unpinned so it defaults to C# 15.
  - `Nullable` `enable`.
  - `ImplicitUsings` `enable`.
- **Warnings and analyzers:**
  - `TreatWarningsAsErrors` `true`. This also turns NuGetAudit findings into errors.
  - `AnalysisLevel` `latest-recommended`.
  - `NuGetAuditMode` `all`.
  - `EnforceCodeStyleInBuild` `true`, so `.editorconfig` rules are checked by the build.
  - `GenerateDocumentationFile` `true` for every project. Tests suppress CS1591: their names are the specification.
- **Package management.** Central Package Management is enabled.
- **Library-only settings:**
  - `IsPackable`, `IsAotCompatible` (which implies `IsTrimmable`), and package metadata.
  - The version is `0.1.0-preview` until the GA switch in M8, because a stable package can't depend on prerelease build inputs.
  - `ContinuousIntegrationBuild` in CI.
  - SourceLink, which the SDK provides, with no package reference needed.
  - Symbol packages (`snupkg`).
- **ConfigureAwait.** Internal awaits in the library use `ConfigureAwait(false)`. Arbitrator never awaits before calling into user code, so a behavior or handler starts on the caller's context.

### 9.4 Dependencies

The audit was performed on 2026-11-07. Versions came from the NuGet V3 flat-container index. Advisories were checked against NuGet's vulnerability feed (`https://api.nuget.org/v3/vulnerabilities/index.json`, the same data NuGetAudit uses). As a control, the same query returned the known advisories for `System.Text.Json` 8.0.0. NuGetAudit runs again on every restore (§9.3).

| Package | Used by | Version | Purpose | Why not build it in | License | Maintenance | Advisories |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Microsoft.Extensions.DependencyInjection.Abstractions | Not referenced | n/a | `IServiceCollection`, `ServiceDescriptor`, keyed services | Ships in the .NET 11 shared framework (`Microsoft.NETCore.App`); a package reference fails restore with NU1510 | MIT | Microsoft | n/a |
| Microsoft.Extensions.DependencyInjection | Tests only | `11.0.0-rc.3.26522.148` now, `11.0.0` at GA | A real container for scope, disposal, and validation tests | Tests must exercise the real container | MIT | Microsoft | None |
| TUnit | Tests only | `1.81.10` (latest stable) | The test framework on Microsoft.Testing.Platform. Brings `TUnit.Core`, `TUnit.Engine`, `TUnit.Assertions` 1.81.10 and `Microsoft.Testing.Platform` 2.5.2 | Not reasonable to write a test framework | MIT | Active | None, including transitive dependencies |
| BenchmarkDotNet | Benchmarks only (optional, M9) | `0.15.8` (latest stable) | Measures dispatch overhead and allocations | Not reasonable to write a benchmark harness | MIT | Active, de facto standard | None. Support for `net11.0` must be verified in M9 (§12) |
| Microsoft.Extensions.Logging.Abstractions | Not referenced | n/a | `ILogger` and the `[LoggerMessage]` generator for the logging example | Ships in the .NET 11 shared framework, including the source generator | MIT | Microsoft | n/a |

No assertion or mocking packages are added. TUnit's assertions and hand-written test doubles are enough.

### 9.5 Versioning and compatibility

- The version follows SemVer. Any 0.x release may break the API. Breaking changes are listed in the release notes.
- Nothing is published until the package references and SDK are the stable `11.0.0`/`11.0.100` releases.

### 9.6 License

The owner chose **Apache-2.0**. `LICENSE` holds the unmodified license text, and the package declares the `Apache-2.0` license expression. Every shipped and test dependency is MIT or Apache-2.0, so all are compatible. The license has no NOTICE file because the project has no third-party notices to carry.

---

## 10. Test strategy and acceptance criteria

### 10.1 Approach

- **Black-box tests through the public API.** Every test builds its own `ServiceCollection` and runs against the real MS.DI container. No state is shared between tests.
- **Journal.** Test doubles append events to a per-test `Journal` service, and tests assert on the resulting sequence.
- **Timing.** No test relies on sleeps or wall-clock timing. Coordination uses `TaskCompletionSource`.
- **Compile-time fixtures.** The inference table in §4.3 and the examples in §4.4 are compiled in the test project. They fail the build if they drift from the contracts.
- **Mapping.** Each row of §7 maps to at least one test.

### 10.2 Acceptance criteria

**Typed dispatch**
- **D1.** `SendAsync(new GetCustomer(id))` returns the very instance the handler returned (`ReferenceEquals`). The handler received the caller's request instance.
- **D2.** A request held as `IRequest<CustomerDto?>` dispatches to the concrete handler, and the journal is identical to D1's.
- **D3.** A struct request round-trips its payload.
- **D4.** A class implementing two handler contracts serves both request types. Two sends observe two distinct handler instances.
- **D5.** Sending a type derived from a handled request faults with `ArbitratorConfigurationException`. The message names both types, and the base type's handler isn't invoked.
- **D6.** A request whose handler was registered directly with `services.AddTransient<IRequestHandler<X, Y>, H>()` faults as a missing handler.

**No-response dispatch**
- **N1.** `SendAsync(new DeactivateCustomer(...))` has the static type `Task` and completes after the handler's task. The handler ran once.
- **N2.** An open behavior observes `typeof(TResponse) == typeof(Unit)`, and a closed `IPipelineBehavior<DeactivateCustomer, Unit>` runs.
- **N3.** Calling a handler that implements `IRequestHandler<X>` through `IRequestHandler<X, Unit>` returns `Unit.Value` after its `Task` completes, and it propagates that task's exception unchanged.
- **N4.** A behavior that short-circuits a no-response request with `Unit.Value` completes the send without invoking the handler.

**Pipeline ordering and short-circuiting**
- **P1.** Registering B1 and then B2 produces the journal `B1.before, B2.before, H, B2.after, B1.after`.
- **P2.** Open and closed behaviors registered interleaved across two `AddArbitrator` calls run in global registration order, as in the §5.6 example.
- **P3.** A constrained open behavior runs for requests that satisfy its constraint and is absent for others. A constraint that refers to `TResponse` is honored.
- **P4.** In a short-circuit, inner behaviors and `HandleAsync` aren't invoked, and the caller receives the behavior's value.
- **P5.** The order of handler and behavior registration calls doesn't change the pipeline.
- **P6.** A second call to `next` throws `InvalidOperationException` naming the behavior. Inner components ran exactly once. Two concurrent invocations result in exactly one execution.
- **P7.** A null `Task` from a behavior or handler faults the send with `InvalidOperationException` naming that component.

**Cancellation token propagation**
- **C1.** Every behavior and the handler observe a token equal to the caller's token.
- **C2.** A substituting behavior's inner components observe the substituted token, while outer components still hold the original.
- **C3.** A token that's already canceled produces a task in the `Canceled` state, with `OperationCanceledException.CancellationToken` equal to the caller's token. No handler or behavior constructor ran, which a constructor counter shows.
- **C4.** With a token that's already canceled and a missing handler, the send faults with `ArbitratorConfigurationException`.
- **C5.** A handler that awaits `Task.Delay(Timeout.Infinite, token)` is canceled after it signals that it started. The send ends `Canceled`, and every behavior's `finally` ran.
- **C6.** A handler that ignores the token returns its result even though the caller canceled during execution.

**Exception identity and stack preservation**
- **E1.** A handler throws exception instance X. The awaited send throws X (`ReferenceEquals`), X isn't wrapped, and X's stack trace contains the handler's `HandleAsync` frame.
- **E2.** When a behavior throws before `next`, the inner components weren't invoked and the same instance propagates.
- **E3.** A handler constructor that throws X surfaces X unwrapped, not as `TargetInvocationException`, and no behavior ran.
- **E4.** `SendAsync(null!)` returns a faulted task without throwing synchronously.
- **E5.** Arbitrator's configuration exception is distinguishable from a handler's `InvalidOperationException` by its type.

**Duplicate and missing registrations**
- **R1.** Each registration error in §8.2 throws `ArbitratorConfigurationException` from the offending builder call. The message contains the named types. The collection is unchanged by the failing call.
- **R2.** After two `AddArbitrator` calls, the collection has exactly one `ISender` descriptor, and registrations from both calls are effective.
- **R3.** `BuildServiceProvider(ValidateOnBuild = true)` fails, naming the component, when a handler, a closed behavior, or an open behavior's instantiation for a registered request has an unresolvable dependency.
- **R4.** A singleton that depends on `ISender` fails container validation when `ValidateScopes` is enabled.
- **R5.** A closed behavior with no matching handler makes `ISender` resolution throw `ArbitratorConfigurationException`.
- **R6.** A builder call after `ISender` has been resolved throws `ArbitratorConfigurationException`.
- **R7.** An application registration of a handler's implementation type with a different lifetime doesn't affect dispatch: a new instance is still created for every send.

**Scope identity and isolation**
- **S1.** The scoped dependency a handler receives is the instance the test resolves from the same scope. This also proves that no hidden scope was created.
- **S2.** Two scopes yield different scoped instances.
- **S3.** A nested send from a handler observes the same scoped instance as the outer handler, and the nested send's pipeline runs (it appears in the journal).
- **S4.** Resolving `ISender` from the root provider with `ValidateScopes` enabled throws MS.DI's `InvalidOperationException`.

**Disposal ownership**
- **L1.** A disposable transient handler isn't disposed when `SendAsync` completes. It's disposed exactly once when its scope is disposed.
- **L2.** A behavior that implements only `IAsyncDisposable` is disposed on `DisposeAsync` of the scope.
- **L3.** The scope remains usable after a send that faulted, and Arbitrator disposed nothing.

**Concurrent execution**
- **X1.** 1,000 sends across 1,000 scopes, run with `Parallel.ForEachAsync`, each carry a unique payload. Every response matches its request, and every behavior instance is distinct.
- **X2.** 100 concurrent sends in one scope succeed, each with a distinct handler instance.
- **X3.** 64 concurrent first sends on a fresh provider all succeed with identical pipelines.

### 10.3 Optional performance benchmarks (M9)

These are kept separate from correctness tests and have no pass or fail targets. Their results are used only to compare one revision with another.

- The overhead of `SendAsync` compared with calling the handler directly, with 0, 1, and 3 behaviors.
- Allocations per send, measured with `MemoryDiagnoser`.
- The cost of building the dispatch table for 10, 100, and 1,000 handlers.

---

## 11. Implementation plan

| Milestone | Deliverables | Done when |
| --- | --- | --- |
| **M0 Foundation** | `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, the `src/Arbitrator` and `tests/Arbitrator.Tests` projects in `Arbitrator.slnx` | `dotnet build` has zero warnings. `dotnet test` runs a smoke test that loads the library |
| **M1 Contracts** | Every type in §4.1, with XML documentation | The §4.3 inference fixtures compile, and N3 passes |
| **M2 Registration** | `AddArbitrator`, `ArbitratorBuilder`, the registry, validators, applicability, keyed descriptor emission, repeated-call handling | R1, R2, R3, R4, and R7 pass |
| **M3 Dispatch core** | `Sender`, the dispatch table and freezing, invokers, missing-handler diagnostics, null and cancellation entry checks | D1–D6, N1, C3, C4, E1, E3, E4, E5, and R6 pass |
| **M4 Pipeline** | Step composition, ordering, constrained applicability, short-circuiting, the continuation guard, the null-`Task` check, token substitution, the closed-behavior-with-no-handler check | N2, N4, P1–P7, C1, C2, C5, C6, E2, and R5 pass |
| **M5 Lifecycle and concurrency** | Scope, disposal, nested-send, and concurrency tests, with fixes where they fail | S1–S4, L1–L3, and X1–X3 pass. Every row of §7 maps to a passing test |
| **M6 Diagnostics and trimming review** | `TypeNameFormatter`, a message review against §8.2, trimming and AOT analyzer warnings resolved by annotation, final placement of `RequiresDynamicCode` | Zero analyzer warnings, no blanket suppressions, message tests pass |
| **M7 Documentation and packaging** | README (quick start, contract summary), the §4.4 examples in `tests/Examples`, package metadata, `LICENSE` (needs §9.6), `dotnet pack` | The package contains its README, XML documentation, and symbols. A throwaway console app consumes it from a local feed and dispatches successfully |
| **M8 Release v0.1.0** | Switch to the stable `11.0.0` packages and `11.0.100` SDK, a clean NuGetAudit, a `v0.1.0` tag, publishing | All tests pass on the GA toolchain and the package is published |
| **M9 Benchmarks (optional)** | `benchmarks/Arbitrator.Benchmarks` | The benchmarks in §10.3 run on `net11.0` |

**Rollout and rollback.** v0.1 is a new package with no existing consumers to migrate and no persisted state. If a defect is found after publishing, the release is unlisted on NuGet and fixed in 0.1.1. Consumers roll back by pinning the previous version.

---

## 12. Risks and unresolved decisions

| Risk or decision | Recommended default | What would change it |
| --- | --- | --- |
| .NET 11 isn't GA yet, and the packages are `rc.3` | Develop against `rc.3` and publish only on the GA toolchain (M8) | Breaking changes in an RC would require adjustment. GA is expected imminently |
| Naming: `SendAsync` and `HandleAsync` versus `Send` and `Handle` | The `Async` suffix, following the owner's standard | The owner preferring terse names. Decide before M1 to avoid churn |
| Dependence on keyed services | Use them. MS.DI is the only supported container in v0.1 | A requirement for another container whose MS.DI adapter lacks `IKeyedServiceProvider` would lead to self-registration with uniqueness enforced at registration |
| The continuation guard blocks retry behaviors | Keep the guard | Real retry needs inside the pipeline, combined with acceptance that a retry reuses the same scoped state. Removing the guard isn't a breaking change |
| Transient-only lifetimes | Transient | Profiling showing that construction cost matters, or a real need for scoped handler state. Lifetime overloads can be added without breaking anyone |
| A closed behavior with no handler is detected at first `ISender` resolution, not at build | Fail at first resolution, and recommend the application-side test in §6.5 | Owner preference for treating such behaviors as inert |
| Constraint semantics ignore `notnull` and nullable annotations | Supported as specified, with the limitation documented | Confusion seen in practice would lead to excluding constrained open behaviors and checking marker interfaces inside the behavior instead |
| The name `Unit` collides with `System.Reactive.Unit`, `MediatR.Unit`, and others when both namespaces are imported | Keep `Unit`. Callers rarely name it | Collisions reported in real use would lead to a rename, such as `NoResponse`, before 1.0 |
| `RequiresDynamicCode` and `RequiresUnreferencedCode` warnings bother trimming- and AOT-aware consumers | Accept it for v0.1 | A consumer that must publish Native AOT would justify the generator path in §13 |
| One `Task` and step objects are allocated per dispatch | Accept them | Benchmark evidence from M9 justifying pooling or other specialization |
| Descriptors grow with handlers × open behaviors | Accept it (each is a small descriptor) | Measured slow startup at thousands of handlers |
| BenchmarkDotNet 0.15.8 may not recognize `net11.0` | Verify in M9, and use 0.16 once it's stable | Not on the critical path |
| CA1716 flags any abstract or virtual behavior method whose continuation parameter is named `next` | Keep `next`, suppressed once on the interface | Consumers who write abstract behavior base classes see CA1716 too; renaming the parameter to `continuation` before 1.0 would remove it everywhere |

---

## 13. Deferred roadmap

| Feature | What would justify adding it |
| --- | --- |
| **Notifications** (planned for v0.2) | An application needs in-process fan-out, such as domain events. The semantics would be: `IPublisher.PublishAsync` is awaited; handlers run sequentially in registration order; the first exception stops publication and propagates; zero handlers counts as success; request behaviors don't apply. There would be no parallel and no fire-and-forget publishing. This would be in-process dispatch only, with no durability, transactional publication, or outbox |
| Configurable lifetimes | Measured construction cost, or a real need for scoped or singleton handler state |
| Registry inspection API | Applications want a test asserting that every `IRequest<>` type in an assembly has a handler |
| `Arbitrator.Abstractions` package | Contract-only assemblies must avoid the DI abstractions dependency, or a second container integration appears |
| Source generator and Native AOT | A consumer must publish with Native AOT |
| Streaming requests (`IAsyncEnumerable`) | A consumer needs incremental results through the pipeline |
| Opt-in assembly scanning helper | Real applications find explicit lists to be a demonstrated maintenance burden. It would never become the default |
| Other containers | A concrete user on that container |
| Polymorphic dispatch | Not planned. Exact-type dispatch is a core opinion |

---

## Ready to implement when…

- [x] **The owner confirms `SendAsync`/`HandleAsync` naming.** Approved.
- [x] **The owner accepts the keyed-service registration approach (decision 11).** Approved.
- [x] **The owner accepts deferring notifications to v0.2.** Not contested; notifications remain deferred.
- [x] **The owner chooses a license (§9.6).** Apache-2.0.
- [ ] **The .NET 11 GA packages are available.** This blocks M8 (publishing) only.

All other decisions in §3 are made in this document and can be implemented without further input.
