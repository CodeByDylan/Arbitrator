# Arbitrator contract

This document states what Arbitrator promises, as of the current version. It is normative: a change to observable
behavior updates it in the same pull request. Every row of the behavioral contract (§6) has at least one test.

It doesn't repeat what other sources already own:

- **Why** something is the way it is: the decision records in [`decisions/`](decisions/).
- **How** it's built: [`architecture.md`](architecture.md). Internals may change in any release.
- **The exact public API:** `src/Arbitrator/PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, which the build
  checks (§8).
- **Worked examples:** `README.md`, and `tests/Arbitrator.Tests/Examples/`, which compiles and runs them.

---

## 1. Principles

These are the opinions that set Arbitrator apart from other mediators:

1. **One way to do each thing.** Each request has one handler, there's one pipeline abstraction for each kind of
   message, one registration entry point, and one ordering rule.
2. **Explicit over discovered.** Only types named in `AddArbitrator`, or handlers found by a scan of an assembly named
   there, take part in dispatch. Scanning is opt-in, each call names its assembly, and behaviors are never discovered.
   `IRequestHandler<,>` services registered directly on the `IServiceCollection` are ignored.
3. **Fail at the earliest stage that can know.** The stages, in order, are registration, container validation, first
   sender resolution, and dispatch. §5.5 lists which failures each stage can detect.
4. **The caller owns the scope, and DI owns lifetimes.** Arbitrator never creates a scope, never disposes a service,
   and never caches service instances. The only things it disposes are the enumerator it obtains while reading a
   stream and the token source it links for that read.
5. **Stay out of the exception path.** Arbitrator never catches, wraps, logs, translates, or retries exceptions from
   behaviors, handlers, or DI activation.
6. **Forward cancellation without interpreting it.** Arbitrator checks the token once at the entry point and otherwise
   only passes it along.
7. **No static mutable state.** Every piece of state belongs to a service collection or a service provider, so tests
   and multiple hosts in one process stay isolated.
8. **Start strict, relax later.** Relaxing a strict rule later doesn't break anyone, while tightening a loose one does.
   That's why Arbitrator guards the continuation, uses transient-only lifetimes, and rejects duplicates.

## 2. Out of scope

**Deferred.** Features that could be added later are tracked as
[enhancement issues](https://github.com/CodeByDylan/Arbitrator/issues?q=is%3Aissue+label%3Aenhancement), each stating
what would justify adding it.

**Not planned.** Polymorphic handler resolution, since exact-type dispatch is a core opinion. Object-based dispatch
(`Send(object)`). Parallel notification publishing. Built-in retries, validation, logging, transactions, or EF Core
integration. A prescribed result or error type. Distributed messaging, persistence, or outbox. MediatR API
compatibility. Assembly scanning as the default registration style.

## 3. Public API

### 3.1 Contracts

The contracts live in the `Arbitrator` namespace: `IRequest<TResponse>`, `IRequest`, `IRequestHandler<TRequest,
TResponse>`, `IRequestHandler<TRequest>`, `IPipelineBehavior<TRequest, TResponse>`, `RequestContinuation<TResponse>`,
`IStreamRequest<TItem>`, `IStreamRequestHandler<TRequest, TItem>`, `IStreamPipelineBehavior<TRequest, TItem>`,
`StreamContinuation<TItem>`, `ISender`, `INotification`, `INotificationHandler<TNotification>`,
`INotificationBehavior<TNotification>`, `NotificationContinuation`, `IPublisher`, `Unit`, and
`ArbitratorConfigurationException`. The registration description (§5.6) adds `ArbitratorRegistrations`,
`RequestRegistration`, and `NotificationRegistration`, which are read-only data. The contracts' shape follows these
rules:

- **Async naming and `Task`.** Methods are `SendAsync`, `StreamAsync`, `PublishAsync`, and `HandleAsync`, and return
  `Task` or `Task<TResponse>`, never `ValueTask`. The stream contracts return `IAsyncEnumerable<TItem>` instead.
- **Invariance.** None of the interfaces declares variance. Contravariant handlers would suggest polymorphic dispatch,
  which Arbitrator deliberately doesn't do. A covariant `IRequest<out TResponse>` would let
  `SendAsync<object>(getCustomer)` compile against a handler that returns `CustomerDto`.
- **The `TRequest : IRequest<TResponse>` constraint on handlers and behaviors.** A handler or closed behavior whose
  response type doesn't match its request is a compile error. Open behaviors must restate the constraint, and the
  compiler enforces that (CS0314). The stream contracts carry `TRequest : IStreamRequest<TItem>` in the same way, and
  the notification contracts carry `TNotification : INotification`.
- **Bridging no-response handlers to `Unit`.** `IRequest` inherits `IRequest<Unit>`, and `IRequestHandler<TRequest>`
  inherits `IRequestHandler<TRequest, Unit>`. It hides the base `HandleAsync` with `new` and implements it explicitly
  as a default interface method, so a class implementing `IRequestHandler<DeactivateCustomer>` writes only
  `Task HandleAsync(...)`. Arbitrator calls it through `IRequestHandler<DeactivateCustomer, Unit>`, and the bridge
  supplies `Unit.Value`. The bridge reports a null task from the handler as the same `InvalidOperationException` that
  response handlers get (§6 row 17). A class may implement `IRequestHandler<TRequest, Unit>` directly instead, and
  Arbitrator treats that the same way.
- **Default for `cancellationToken`.** The token has a default value only on `ISender` and `IPublisher`, as a
  convenience for callers. Handlers and behaviors must accept it explicitly.
- **Why `Unit` is public.** Behaviors observe `Unit` as `TResponse`, and a closed behavior for a no-response request
  must name it, for example `IPipelineBehavior<DeactivateCustomer, Unit>`.
- **One request contract per request type.** A request type implements exactly one of `IRequest`,
  `IRequest<TResponse>`, or `IStreamRequest<TItem>`. Registering a handler for a request type with several is an
  error.

### 3.2 Registration

`services.AddArbitrator(configure)` is the only entry point. It lives in the `Microsoft.Extensions.DependencyInjection`
namespace so that the implicit usings of ASP.NET Core and Worker projects find it. `configure` receives an
`ArbitratorBuilder`:

- `AddHandler<THandler>()` registers every closed `IRequestHandler<,>` and `IStreamRequestHandler<,>` contract that
  `THandler` implements.
- `AddBehavior<TBehavior>()` registers a closed behavior, which applies to every `IPipelineBehavior<TRequest,
  TResponse>`, `IStreamPipelineBehavior<TRequest, TItem>`, and `INotificationBehavior<TNotification>` it implements.
- `AddOpenBehavior(typeof(X<,>))` registers an open-generic behavior, which implements exactly one of
  `IPipelineBehavior<,>` and `IStreamPipelineBehavior<,>` with two type parameters, or `INotificationBehavior<>` with
  one, for example `AddOpenBehavior(typeof(HandlerTimingBehavior<>))`.
- `AddNotificationHandler<THandler>()` registers every closed `INotificationHandler<>` contract `THandler` implements,
  after every notification handler registered before it. Any number of handler types may handle one notification
  type.
- `AddHandlersFromAssembly(assembly)` and `AddHandlersFromAssemblyContaining<TMarker>()` register every request handler,
  stream handler, and notification handler class of one assembly, optionally only those a `Func<Type, bool>` filter
  accepts (§4.4).

Each builder call either completes entirely or changes nothing: it validates first and only then mutates. The methods
return the builder so calls can be chained. `THandler`, `TBehavior`, and `openBehaviorType` carry
`[DynamicallyAccessedMembers(PublicConstructors | Interfaces)]`.

### 3.3 Call-site inference

Each row was checked against the .NET 11 compiler.

| Call site | Overload chosen | Static result type |
| --- | --- | --- |
| `sender.SendAsync(new GetCustomer(id))` where `GetCustomer : IRequest<CustomerDto?>` | Generic; `TResponse` = `CustomerDto?` | `Task<CustomerDto?>` |
| `sender.SendAsync(new DeactivateCustomer(id, reason))` where `DeactivateCustomer : IRequest` | `SendAsync(IRequest)`. `IRequest` is a better conversion target than `IRequest<Unit>` | `Task` |
| `IRequest<CustomerDto?> request = ...; sender.SendAsync(request)` | Generic | `Task<CustomerDto?>` |
| `IRequest command = ...; sender.SendAsync(command)` | `SendAsync(IRequest)` | `Task` |
| `IRequest<Unit> request = ...; sender.SendAsync(request)` | Generic; `TResponse` = `Unit` | `Task<Unit>` (legal, but not the documented style) |
| `readonly record struct Ping : IRequest<int>` | Generic; the request is boxed at the call | `Task<int>` |
| `sender.SendAsync<object>(new GetCustomer(id))` | Compile error, because `IRequest<>` is invariant | n/a |
| A type that implements both `IRequest<A>` and `IRequest<B>` | Compile error CS0411 unless `TResponse` is given explicitly. Registering its handler fails anyway (§7.2) | n/a |
| `object request = ...; sender.SendAsync(request)` | Compile error. There is no object-based dispatch | n/a |
| `sender.StreamAsync(new SearchCustomers(text))` where `SearchCustomers : IStreamRequest<CustomerDto>` | `StreamAsync`; `TItem` = `CustomerDto` | `IAsyncEnumerable<CustomerDto>` |
| `IStreamRequest<CustomerDto> request = ...; sender.StreamAsync(request)` | `StreamAsync` | `IAsyncEnumerable<CustomerDto>` |
| `readonly record struct Countdown : IStreamRequest<int>` | `StreamAsync`; the request is boxed at the call | `IAsyncEnumerable<int>` |
| `sender.StreamAsync<object>(new SearchCustomers(text))` | Compile error, because `IStreamRequest<>` is invariant | n/a |
| `sender.SendAsync(new SearchCustomers(text))` | Compile error. A stream request isn't an `IRequest` | n/a |

The static type at the call site only chooses `TResponse`. The handler and pipeline are always selected by
`request.GetType()`.

## 4. Execution semantics

### 4.1 Sending a request

`SendAsync` is an `async` method, so each failure below arrives as a faulted or canceled `Task`, never as a synchronous
throw.

1. `request` is null → `ArgumentNullException`. Nothing is resolved.
2. No handler is registered for `request.GetType()` → `ArbitratorConfigurationException` (missing handler). Nothing is
   resolved.
3. `cancellationToken` is already canceled → `OperationCanceledException` carrying the caller's token. Nothing is
   resolved.
4. Every applicable behavior is resolved, outermost first, then the handler, from the scope that resolved the
   `ISender`. DI exceptions propagate unwrapped, before any behavior runs.
5. The outermost component is invoked with the caller's token. Each `next` continuation passes on the token it was
   given.
6. The result, or the exception, of the outermost component passes through unchanged. `SendAsync(IRequest)` discards
   the `Unit`.

### 4.2 Behavior applicability and order

A behavior applies only to handlers of its own kind: `IPipelineBehavior<,>` to `IRequestHandler<,>`,
`IStreamPipelineBehavior<,>` to `IStreamRequestHandler<,>`, and `INotificationBehavior<>` to
`INotificationHandler<>`. A notification behavior applies to a notification type as a request behavior applies to a
handler contract, with the notification type as its only type argument. Within its kind, a behavior applies to a
handler contract (`TRequest`, `TResponse`) as follows:

- **A closed behavior** applies when it implements the behavior contract for (`TRequest`, `TResponse`) exactly.
- **An open behavior** applies when closing it over (`TRequest`, `TResponse`) passes the runtime's generic-constraint
  check. The runtime enforces `class`, `struct`, `new()`, base-class, and interface constraints, including constraints
  that refer to the other type parameter. It doesn't enforce `notnull` or nullable reference annotations, and
  `unmanaged` is enforced only as `struct`, so applicability ignores all of these.

Applicable behaviors run in one global registration order, across all `AddArbitrator` calls, with open and closed
behaviors interleaved. Behaviors of every kind share that order and its positions. The first registered is the
outermost. The order of handler calls relative to behavior calls doesn't matter.

For example, suppose these registrations are made in this order, across two `AddArbitrator` calls:

1. `AddOpenBehavior(typeof(LoggingBehavior<,>))`. First call.
2. `AddBehavior<AuditDeactivationBehavior>()`, which implements `IPipelineBehavior<DeactivateCustomer, Unit>`. First
   call.
3. `AddOpenBehavior(typeof(ValidationBehavior<,>))`, constrained to `IValidatable`. Second call.

If each behavior writes a line before and after `next`, sending `DeactivateCustomer` prints:

```text
Logging.before
  Audit.before
    Validation.before
      DeactivateCustomerHandler
    Validation.after
  Audit.after
Logging.after
```

`GetCustomer` doesn't implement `IValidatable` and isn't audited, so sending it prints `Logging.before`, then
`GetCustomerHandler`, then `Logging.after`. If the handler throws, every `after` line in a `finally` still runs, from
the innermost behavior outward, and the caller receives the handler's exception instance.

### 4.3 Publishing a notification

- **Selection.** `IPublisher.PublishAsync` reaches every handler registered for the notification's exact runtime type.
  Handlers for base types or interfaces don't run, so a derived notification can reach no handlers at all; sealed
  notification types are recommended.
- **Order.** Handlers run one after another, in the order of their registration across all `AddArbitrator` calls. Each
  is awaited before the next starts.
- **Steps.**
  1. A null notification faults the task with `ArgumentNullException`.
  2. If no handler is registered for the runtime type, publishing succeeds immediately, without inspecting the token.
  3. Otherwise the token is checked. Then, for each handler in run order, its applicable notification behaviors are
     resolved, outermost first, followed by the handler, all from the publisher's scope and before anything runs.
     Each handler call gets its own behavior instances.
  4. The token is checked again before each handler's chain, that is, before its outermost behavior.
  5. The outermost component of the chain is called with the notification, the handler's implementation type (for
     behaviors), and the token. A behavior or handler returning a null task is reported by name. The first exception
     that reaches the outermost component stops publication and reaches the caller unwrapped, and handlers that
     already ran keep their effects.
- **Notification behaviors.** An `INotificationBehavior<TNotification>` wraps each handler call separately: with
  three handlers, the chain runs three times. A behavior that doesn't invoke `next` skips its handler, which was
  already constructed, and publication continues. A behavior that catches its handler's exception and completes
  normally lets publication continue; Arbitrator imposes nothing. `next` may be invoked at most once. Request
  behaviors never wrap notification handlers, and notification behaviors never wrap request or stream handlers.
- **Context.** After every handler's chain except the last, Arbitrator resumes on the caller's captured context, so
  the outermost component of every chain starts on the context the caller published from. Where inner components
  start depends on how the behaviors await. Arbitrator doesn't return to that context after the last chain, so a
  single-handler publish can't deadlock a caller that blocks on it. Publishing must be awaited: with several handlers,
  a caller blocking on a single-threaded context deadlocks.

### 4.4 Assembly scanning

- **Candidates.** A scan reads every type the assembly defines. Classes count whatever their visibility, including
  nested ones. A class is a candidate when it is concrete, contains no unbound generic parameters, isn't marked
  `CompilerGenerated`, and implements a request handler, stream handler, or notification handler contract, directly or
  through a base class. Stream handlers are registered as request handlers. Other types are skipped without an error.
  Attributes are read only for classes that implement a handler contract. A candidate with a request or stream handler
  contract and a notification handler contract is registered as both. Behaviors are never registered.
- **Order.** Candidates are sorted by full type name, ordinally. The filter, when given, sees each candidate once in
  that order. Accepted request handlers are registered first, then accepted notification handlers, each in that
  order. Scanned notification handlers therefore run in full-name order, after handlers registered before the scan
  call.
- **Rules.** Every accepted type is validated exactly as `AddHandler` and `AddNotificationHandler` validate it, with
  the same exceptions. A type that is already registered is a duplicate, so scanning one assembly twice fails.
- **Errors specific to scanning.** Each throws `ArbitratorConfigurationException` naming the assembly by its simple
  name:
  - types that can't be loaded (the `ReflectionTypeLoadException` is the inner exception)
  - attributes of a handler class that can't be loaded because their assembly is missing (the original exception is
    the inner exception)
  - an assembly with no candidates
  - a filter that excludes every candidate

  Exceptions from the filter propagate unchanged.
- **Atomicity.** A scan registers all accepted types or none.

### 4.5 Concurrency

- Arbitrator's own state is immutable once the first `ISender` or `IPublisher` is resolved, so concurrent sends and
  publishes are safe, including concurrent first resolutions.
- No two sends share request-specific state. Handlers and behaviors are transient, so concurrent sends within one
  scope get distinct instances.
- Arbitrator doesn't make **scoped dependencies** thread-safe. Two concurrent sends in one scope share that scope's
  `DbContext`, for example, and concurrent use of it is the caller's responsibility.

### 4.6 Nested sends

A handler or behavior may inject `ISender` or `IPublisher` and send or publish again:

- **Same scope.** The nested call runs in the same scope, so it shares scoped services and the same unit of work.
- **Full pipeline.** A nested send goes through its own complete pipeline, so outer behaviors such as logging see it
  as a separate send.
- **What isn't guaranteed:**
  - No recursion or cycle detection. Unbounded recursion ends in stack or memory exhaustion.
  - No ambient "parent request" context.
  - No coordination of transactions or ordering beyond what the code itself awaits.
  - No protection when nested sends run concurrently against shared scoped services.

### 4.7 Trimming and Native AOT

Native AOT isn't supported. Registration inspects interfaces through reflection and closes generic types at runtime,
some over value types such as `Unit`, so explicit registration isn't evidence of AOT compatibility.

- `AddHandler`, `AddOpenBehavior`, and `AddNotificationHandler` carry `RequiresDynamicCode` and
  `RequiresUnreferencedCode`. `AddNotificationHandler` closes open notification behaviors over its notification types.
- The four scanning methods carry both: they enumerate an assembly's types at runtime, and trimming can remove handler
  types that nothing else references.
- `AddBehavior` carries neither.

The library builds with `IsAotCompatible`, no trimming or AOT analyzer warnings, and no blanket suppressions.

### 4.8 Streaming requests

- **Laziness.** `ISender.StreamAsync` only creates the stream object. It validates nothing, resolves nothing, inspects
  no token, and never throws. Each read of that object, started by `GetAsyncEnumerator` or `await foreach`, is a
  separate, complete dispatch, so reading a stream twice runs the handler twice.
- **Effective token.** It's the `StreamAsync` token, unless that is the default token, in which case it's the read's
  token. When both are non-default and differ, it's a linked token, created for the read and disposed when the read
  ends, exactly as `[EnumeratorCancellation]` combines tokens for C# iterators.
- **Steps of a read.** They run on the first `MoveNextAsync`, and each failure is thrown from it:
  1. `request` is null → `ArgumentNullException`. Nothing is resolved.
  2. No stream handler is registered for `request.GetType()` → `ArbitratorConfigurationException`. Nothing is
     resolved.
  3. The effective token is already canceled → `OperationCanceledException` carrying it. Nothing is resolved.
  4. Every applicable stream behavior is resolved, outermost first, then the handler, from the scope that resolved the
     `ISender`. DI exceptions propagate unwrapped, before any component runs.
  5. The outermost component is invoked with the request and the effective token, and its stream is read with the
     same token. Each item reaches the caller as it arrives, in order and unchanged. Arbitrator holds at most one item.
- **The pipeline.** A stream behavior wraps the whole read: code before its loop runs before the first item is
  requested, and code after it runs after the inner stream ends. It may pass items through, transform, filter, or add
  them, substitute the token it passes to `next`, or produce items without invoking `next`. `next` may be invoked at
  most once, and the stream it returns may be read at most once.
- **Cancellation.** The effective token is checked once per read, after the handler lookup, and is otherwise only
  forwarded. Arbitrator never checks it between items.
- **Early exit.** When the caller stops reading, Arbitrator disposes the outermost component's enumerator and requests
  no further item. Components that read with `await foreach` dispose their inner enumerators in turn, so every
  component's `finally` blocks run, innermost first.
- **Scope.** The read must happen inside the lifetime of the scope that resolved the `ISender`. A read after the scope
  is disposed fails with DI's `ObjectDisposedException`, and a scope disposed during a read isn't guarded.
- **Context.** Arbitrator calls each component synchronously from the caller's `MoveNextAsync`, so each step of the
  outermost component starts on the context from which the caller requested the item.
- **Catching while yielding.** C# rejects `yield return` inside a `try` block that has a `catch` clause (CS1626). A
  behavior that translates exceptions reads its inner enumerator by hand.

## 5. Dependency injection and lifecycle

### 5.1 Lifetimes

| Service | Lifetime | Registration |
| --- | --- | --- |
| `ISender` | Scoped | Unkeyed. The application may inject it anywhere a scoped service is valid |
| `IPublisher` | Scoped | Unkeyed, like `ISender` |
| `ArbitratorRegistrations` | Singleton | Unkeyed. Describes the registrations of the provider (§5.6) |
| Arbitrator's internal registry and dispatch table | Singleton | Internal types only |
| Handlers, including stream handlers | Transient | Keyed with a private key, registered as their implementation type, one descriptor per handler type |
| Closed behaviors of either kind | Transient | Keyed, registered as their implementation type |
| Open behaviors of either kind | Transient | Keyed. One closed descriptor per applicable registered request of the same kind |
| Notification handlers | Transient | Keyed under a separate private key, registered as their implementation type. A class registered as both a request handler and a notification handler has one registration per role |
| Notification behaviors | Transient | Keyed under a third private key, registered as their implementation type. A closed behavior gets one descriptor; an open one gets one closed descriptor per notification type with handlers that it applies to |

A handler that implements several contracts has one descriptor and still gets a new instance for every send. If the
application also registers a handler type itself, for example `AddScoped<GetCustomerHandler>()`, that registration is
separate and Arbitrator never uses it.

### 5.2 Scopes

- **Which scope dispatch uses.** Dispatch uses exactly the scope that resolved the `ISender` or `IPublisher`.
  Arbitrator never creates a scope. Code without one, such as hosted services, timers, or message consumers, must
  create a scope with `IServiceScopeFactory` and resolve `ISender` from it.
- **Resolving `ISender` from the root provider:**
  - With `ValidateScopes` enabled, MS.DI throws `InvalidOperationException` ("Cannot resolve scoped service
    'Arbitrator.ISender' from root provider."). The host builders enable it in the Development environment.
  - With it disabled, resolution succeeds and everything is resolved from the root for the application's lifetime.
    Scoped dependencies effectively become singletons that are shared across concurrent work, and transient disposable
    handlers and behaviors pile up in the root until shutdown, which is a memory leak.
- **Capturing `ISender` in a singleton** has the same effect. Container validation (`ValidateScopes` with
  `ValidateOnBuild`) reports it at build time, because a singleton depends on a scoped service.
- **No ambient scope.** Arbitrator doesn't consult `AsyncLocal`, `IHttpContextAccessor`, or any similar mechanism.

`IPublisher` behaves exactly like `ISender` in each of these cases.

### 5.3 Disposal

The DI container owns the disposal of services, and Arbitrator never calls `Dispose` or `DisposeAsync` on one:

- Transient disposable handlers and behaviors are disposed when the scope that resolved them is disposed, not when
  `SendAsync` completes or a stream read ends.
- While reading a stream, Arbitrator disposes the enumerator it obtained from the outermost component, when the read
  ends or the caller stops early, and the linked token source it created for the read, if any (§4.8). An enumerator
  that is read by hand and neither finished nor disposed keeps its components referenced until it is collected.
- A long-lived scope that dispatches many requests keeps every disposable transient alive until the scope ends. The
  guidance is one scope per unit of work.
- If a service implements only `IAsyncDisposable`, its scope must be disposed asynchronously. This is MS.DI's rule.

### 5.4 Repeated `AddArbitrator` calls and duplicates

**Repeated calls**

- Repeated calls on the same collection are supported and add to one registry.
- Behavior order is the global order of registration calls across all `AddArbitrator` calls.
- Separate `IServiceCollection` instances have independent registries.

**Duplicates.** Each of the following is an `ArbitratorConfigurationException`, thrown from the offending builder call:

| Situation | Result |
| --- | --- |
| A second handler type for a request type that already has one | Error |
| The same handler type registered twice | Error |
| The same notification handler type registered twice | Error |
| The same behavior type registered twice, open or closed | Error |
| A closed behavior type that equals the instantiation of a registered open behavior for some registered request, or for some notification type with handlers | Error, because the behavior would run twice |

**Other cases**

- **Changes after freezing.** Builder calls made after a provider has attempted to resolve `ISender` or `IPublisher`
  throw `ArbitratorConfigurationException`. This holds even when the attempt failed, so the registrations a provider
  was checked against can't change underneath it.
- **Registrations after a provider was built.** Registrations made after `BuildServiceProvider`, but before that
  provider first resolves `ISender` or `IPublisher`, are accepted. That provider can't resolve them, so its first
  resolution throws `ArbitratorConfigurationException` naming them. Providers that don't implement
  `IServiceProviderIsKeyedService` skip this check.
- **Read-only collections.** Builder calls on a read-only collection, for example after
  `HostApplicationBuilder.Build()`, throw `ArbitratorConfigurationException` before anything changes.
- **Copied collections.** A collection whose descriptors were copied from another collection initially shares the
  original's registry. On its first `AddArbitrator` call it replaces that registry with an independent, unfrozen copy,
  so later registrations on either collection don't affect the other.
- **Handlers registered without Arbitrator.** `IRequestHandler<,>` and `IPipelineBehavior<,>` services registered
  directly on the collection are ignored. Arbitrator neither resolves nor validates them.

### 5.5 Validation boundaries

| Stage | What is detected | What can't be detected here |
| --- | --- | --- |
| **Registration** (each builder call, atomic) | Null arguments. A read-only collection. Invalid handler or behavior shapes, including value-type, generic-parameter, and partially closed behavior types. Abstract request and notification types, and request types with more than one request contract of any kind. Open behaviors implementing more than one behavior contract, or with the wrong number of type parameters for their contract. All duplicate cases. Scanning failures (§4.4). Calls made after freezing | Constructor dependencies. Whether the application has requests without handlers |
| **Container validation** (`ValidateOnBuild`, `ValidateScopes`) | Unresolvable constructor dependencies of every handler, notification handler, and behavior instantiation that applies to a registered request or notification, because all of them are closed keyed descriptors. Singletons that depend on `ISender` or `IPublisher`. Lifetime violations inside application services | Exceptions thrown by constructors. Arbitrator's own cross-registration rules |
| **First `ISender`, `IPublisher`, or `ArbitratorRegistrations` resolution** in a provider | Closed behaviors that match no registered handler contract and no notification type with handlers. Handlers and behaviors registered after the provider was built | Requests that are never registered |
| **Dispatch**, publishing, or the first `MoveNextAsync` of a stream read | Null request or notification. A request type with no handler, including derived types of handled requests. Constructor exceptions. A continuation invoked twice, or its stream read twice. Null `Task` or stream results | n/a |
| **Never** | Arbitrator can't prove that every request type in the application has a handler; applications can check it with `ArbitratorRegistrations` (§5.6). It doesn't check that handlers honor cancellation, or that a non-nullable `TResponse` is actually non-null | |

**Recommended application-side test:** build the real container with `ValidateOnBuild` and `ValidateScopes` enabled,
create a scope, and resolve `ISender`. Together these cover every stage before dispatch. Then compare
`ArbitratorRegistrations.Requests` with the request types your assemblies define, so a request without a handler fails
the test instead of the first send.

### 5.6 Inspecting registrations

`ArbitratorRegistrations` describes what one service provider dispatches.

- **Resolution.** It's an unkeyed singleton, added by the first `AddArbitrator` call. It resolves from the root
  provider, including with `ValidateScopes` enabled, and from any scope, as the same instance. Providers built from
  one collection return the same instance.
- **Freezing.** Resolving it builds the dispatch table, exactly as the first resolution of `ISender` or `IPublisher`
  does: the registrations freeze, and the same checks run and fail with the same exceptions. `ValidateOnBuild` doesn't
  resolve it, so building a provider never freezes anything.
- **`Requests`.** One `RequestRegistration` per request type with a handler, including stream request types:
  - `RequestType`: the exact type dispatch selects the entry for
  - `ResponseType`: the `TResponse` of `IRequest<TResponse>`, `Unit` for `IRequest`, or the `TItem` of
    `IStreamRequest<TItem>`
  - `IsStream`, and `HandlerType`
  - `BehaviorTypes`: the closed behavior types dispatch runs for the request, outermost first. Open behaviors appear
    as the instantiation that runs
- **`Notifications`.** One `NotificationRegistration` per notification type with at least one handler, with its
  `HandlerTypes` in run order and its `BehaviorTypes`, the closed notification behaviors that wrap each handler,
  outermost first.
- **Order.** Both lists are sorted by the ordinal order of `Type.FullName`. Within an entry, types keep dispatch order.
- **Read-only.** The lists are immutable: changing one through a mutable interface such as `IList<T>` throws
  `NotSupportedException`. The types have no public constructors and no value equality. Reading the description
  constructs no handler or behavior.
- **Not for clients.** Like exception messages, it names the application's types, so it's for tests and diagnostics,
  not for responses.

## 6. Behavioral contract

"Arbitrator" in the right-hand column means the library itself enforces the rule. "Forwarded" means Arbitrator only
passes values or exceptions through.

| # | Scenario | Contract | Enforced by |
| --- | --- | --- | --- |
| 1 | Request with a response | The handler runs once and receives the caller's request instance and token. `SendAsync` returns the instance that the outermost component returned | Arbitrator |
| 2 | Request without a response | `SendAsync(IRequest)` completes when the pipeline completes. Behaviors see `TResponse` = `Unit`. Neither the handler nor the caller touches `Unit` | Arbitrator |
| 3 | Null request | The task faults with `ArgumentNullException` (parameter `request`). Nothing is resolved | Arbitrator |
| 4 | Missing handler | The task faults with `ArbitratorConfigurationException`. Nothing is resolved and no behavior runs. Detected before the cancellation check | Arbitrator |
| 5 | Request type derived from a handled type | Treated as a missing handler. The message names the registered base type | Arbitrator |
| 6 | Duplicate handlers | Can't reach dispatch: rejected at registration (§5.4) | Arbitrator |
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
| 25 | Nested send | Permitted. It runs in the same scope through its own full pipeline. No recursion detection (§4.6) | Arbitrator |
| 26 | `ISender` resolved from the root provider | DI throws if scope validation is enabled. Otherwise everything resolves from the root (§5.2) | DI |
| 27 | A handler returns null for a non-nullable `TResponse` | Not checked. The value is returned as is | Not checked |
| 28 | One class implements several handler contracts | Each contract dispatches normally, with a new instance per send | Arbitrator |
| 29 | `SendAsync` called without being awaited | Never throws synchronously. Every failure in this table appears through the returned `Task` | Arbitrator |
| 30 | Notification with no handlers for its runtime type | Publishing succeeds. Nothing is resolved and the token isn't inspected | Arbitrator |
| 31 | Several notification handlers | They run sequentially in registration order, each awaited before the next | Arbitrator |
| 32 | A notification handler throws | The same exception instance reaches the caller. Later handlers don't run, unless a notification behavior catches the exception (row 67); earlier handlers keep their effects | Forwarded |
| 33 | Token canceled before or between notification handlers | The publish is canceled with the caller's token, and the remaining handlers don't run | **Checked by Arbitrator** |
| 34 | Notification derived from a handled notification type | Reaches no handlers, and publishing succeeds | Arbitrator |
| 35 | Publishing or sending from inside a notification handler | Permitted, in the same scope, with no recursion detection | Arbitrator |
| 36 | Assembly scan | Every candidate (§4.4) is registered as a request handler, a notification handler, or both. Nothing else is registered | Arbitrator |
| 37 | Order of scanned notification handlers | Full-name order, ordinal, after handlers registered before the scan call | Arbitrator |
| 38 | Scan with a filter | The filter sees each candidate once, in full-name order. Only accepted types are registered, and its exceptions propagate unchanged | Arbitrator |
| 39 | Scanned type already registered or invalid | The same exception as the explicit registration would throw | Arbitrator |
| 40 | Scan that fails | Registers nothing and adds no descriptors | Arbitrator |
| 41 | Scan that finds no candidates, or whose filter excludes them all | `ArbitratorConfigurationException` naming the assembly | Arbitrator |
| 42 | Scanned assembly with unloadable types, or a scanned handler class whose attributes can't be loaded | `ArbitratorConfigurationException` naming the assembly, with the original exception as the inner exception. Attributes of classes that aren't handlers are never read | Arbitrator |
| 43 | Behaviors in a scanned assembly | Never registered | Arbitrator |
| 44 | Stream request | Each item the handler yields reaches the caller in order, as the same instance, once. The handler receives the caller's request instance and the effective token (§4.8). Nothing is buffered | Arbitrator |
| 45 | Creating a stream | `StreamAsync` never throws and does nothing. Every failure of a read arrives from a `MoveNextAsync` call | Arbitrator |
| 46 | Reading one stream again | Each read is a separate dispatch that resolves new components and runs the pipeline again | Arbitrator |
| 47 | Missing stream handler, including for derived types | `ArbitratorConfigurationException` at the first `MoveNextAsync`, before the cancellation check. Nothing is resolved | Arbitrator |
| 48 | Token canceled when a read starts | The read is canceled with an `OperationCanceledException` carrying the effective token, after the handler lookup. Nothing is resolved | **Checked by Arbitrator, once per read** |
| 49 | Cancellation during a read | Forwarded only, never checked between items | Forwarded |
| 50 | Stream behaviors | Applicable stream behaviors run in global registration order, outermost first, and wrap the entire read. Request behaviors never wrap stream handlers, and stream behaviors never wrap request handlers | Arbitrator |
| 51 | A stream behavior produces items without invoking `next` | Inner components aren't invoked, although they were constructed. The behavior's stream is the result | Arbitrator |
| 52 | A stream handler or behavior returns a null stream | `InvalidOperationException` naming the component and the request type | Arbitrator |
| 53 | `next` invoked twice, or the stream it returned read twice, by a stream behavior | `InvalidOperationException` naming the behavior. Inner components ran once | Arbitrator |
| 54 | An exception while a stream is produced | The same exception instance reaches each enclosing behavior and the caller. Items already yielded stay delivered | Forwarded |
| 55 | The caller stops reading early | The outermost component's enumerator is disposed and no further item is requested. Arbitrator disposes no services | Arbitrator |
| 56 | Activation failure of a stream component | DI's exception propagates unwrapped from the first `MoveNextAsync`, before any component runs | Forwarded |
| 57 | Resolving `ArbitratorRegistrations` | A singleton, from the root provider or any scope, describing what that provider dispatches | Arbitrator |
| 58 | Resolving `ArbitratorRegistrations` before `ISender` | The registrations freeze, and the checks of the first `ISender` or `IPublisher` resolution run, failing with the same exceptions | Arbitrator |
| 59 | Request entries | One per request type with a handler: request type, response or item type (`Unit` for `IRequest`), stream flag, handler type, and the closed behavior types that run, outermost first | Arbitrator |
| 60 | Notification entries | One per notification type with handlers, listing them in run order, and the closed notification behaviors that wrap each, outermost first | Arbitrator |
| 61 | Order of the entries | Both lists are sorted by ordinal full type name | Arbitrator |
| 62 | Changing or reading the description | The lists are immutable, and reading them constructs no handler or behavior | Arbitrator |
| 63 | Notification behaviors | Applicable notification behaviors run around each handler call separately, in global registration order, outermost first | Arbitrator |
| 64 | Arguments of a notification behavior | The publisher's notification instance, the implementation type of the handler it wraps, and the token | Arbitrator |
| 65 | Applicability of notification behaviors | Closed: the exact notification type. Open: the runtime constraint check. Request and notification behaviors never wrap each other's handlers | Arbitrator |
| 66 | A notification behavior doesn't invoke `next` | Its handler, already constructed, is skipped, and publication continues with the next handler | Arbitrator |
| 67 | A notification behavior catches its handler's exception | It may swallow or translate it. If it completes normally, publication continues. Arbitrator imposes nothing | Behavior |
| 68 | `next` invoked twice by a notification behavior | `InvalidOperationException` naming the behavior. The handler runs once | Arbitrator |
| 69 | A notification behavior returns a null `Task` | `InvalidOperationException` naming the behavior and the notification type | Arbitrator |
| 70 | Notification behavior instances | Each handler call gets its own instances, and every behavior and handler is resolved before the first runs | Arbitrator |
| 71 | Cancellation with notification behaviors | Checked before each handler's chain, and otherwise only forwarded | **Checked by Arbitrator** |
| 72 | Notification without handlers | No notification behavior is resolved or run | Arbitrator |

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

## 7. Diagnostics

### 7.1 Exception taxonomy

| Exception | Thrown by | When |
| --- | --- | --- |
| `ArgumentNullException` | Arbitrator | A null `configure`, `openBehaviorType`, `request`, `notification`, `assembly`, or `filter` |
| `ArbitratorConfigurationException` (derives from `InvalidOperationException`) | Arbitrator | Every registration error (§7.2), a closed behavior with no handler when the first `ISender` or `IPublisher` is resolved, a builder call after freezing, and a missing handler at dispatch |
| `InvalidOperationException` | Arbitrator | Misuse of the contract by user code at runtime: a continuation invoked twice, a continuation's stream read twice, or a null `Task` or stream result. These are bugs in a behavior or handler, not configuration problems |
| `OperationCanceledException` | Arbitrator, from the entry check, or user code | The caller's token is already canceled at entry, or the effective token is already canceled when a stream read starts. Any other cancellation comes from user code |
| `InvalidOperationException` from MS.DI | DI | Unresolvable dependencies, or resolving a scoped service from the root |
| Anything else | User code | Propagates unwrapped, as the same instance |

`ArbitratorConfigurationException` is the only custom type. It exists so that hosts and tests can tell "Arbitrator is
misconfigured" apart from an `InvalidOperationException` thrown by a handler. Arbitrator adds no custom types for
cancellation, handler failures, or activation failures, because the existing exceptions already carry the needed
information.

### 7.2 Messages

Type names in messages use C# syntax and include namespaces, for example
`IRequestHandler<Contoso.Customers.GetCustomer, Contoso.Customers.CustomerDto>`. No-response contracts appear as
`IRequestHandler<Contoso.Customers.DeactivateCustomer>`. The wording below is representative. Tests assert on the
exception type and the type names a message contains, not on exact text.

| Failure | Stage | Representative message |
| --- | --- | --- |
| Missing handler | Dispatch | `No handler is registered for request type 'Contoso.Customers.GetCustomer'. Register a class implementing IRequestHandler<Contoso.Customers.GetCustomer, Contoso.Customers.CustomerDto?> with AddHandler<THandler>(). Handlers are matched to the exact runtime type of the request.` |
| Missing handler, base type registered | Dispatch | The missing-handler message, followed by: `A handler is registered for base type 'Contoso.Customers.GetCustomer'; handlers for base types are not used for 'Contoso.Customers.GetVipCustomer'.` |
| Missing stream handler | First `MoveNextAsync` of a read | `No handler is registered for stream request type 'Contoso.Customers.SearchCustomers'. Register a class implementing IStreamRequestHandler<Contoso.Customers.SearchCustomers, Contoso.Customers.CustomerDto> with AddHandler<THandler>(). Handlers are matched to the exact runtime type of the request.` The base-type sentence is appended as for requests |
| Second handler for a request | Registration | `Request type 'Contoso.Customers.GetCustomer' already has handler 'Contoso.Customers.GetCustomerHandler'; cannot also register 'Contoso.Legacy.LegacyGetCustomerHandler'. Each request type has exactly one handler.` |
| Handler type registered twice | Registration | `Handler type 'Contoso.Customers.GetCustomerHandler' is already registered. Each handler type may be registered once across all AddArbitrator calls.` |
| Handler with no contract | Registration | `Type 'Contoso.Customers.CustomerService' cannot be registered as a handler: it implements none of IRequestHandler<TRequest, TResponse>, IRequestHandler<TRequest>, or IStreamRequestHandler<TRequest, TItem>.` |
| Abstract handler | Registration | `Handler type 'Contoso.Customers.HandlerBase' is abstract; register a concrete class.` |
| Request with several request contracts | Registration | `Request type 'Contoso.Customers.Ambiguous' implements more than one request contract (IRequest<A>, IStreamRequest<B>). A request type must implement exactly one of IRequest, IRequest<TResponse>, or IStreamRequest<TItem>.` |
| Handler for an abstract or interface request type | Registration | `Handler 'Contoso.Customers.QueryHandler' handles 'Contoso.Customers.QueryBase', which is abstract or an interface. Requests are dispatched by their exact runtime type, so this handler could never run; register a handler for each concrete request type.` |
| Invalid open behavior | Registration | `Open behavior 'Contoso.Pipeline.AuditBehavior<T>' must be a generic type definition that implements exactly one behavior contract using its type parameters in order: IPipelineBehavior<TRequest, TResponse> or IStreamPipelineBehavior<TRequest, TItem> with two type parameters, or INotificationBehavior<TNotification> with one.` |
| Closed type passed to `AddOpenBehavior` | Registration | `'Contoso.Pipeline.LoggingBehavior<Contoso.Customers.GetCustomer, Contoso.Customers.CustomerDto?>' is a closed type. Register it with AddBehavior<TBehavior>() or pass the generic type definition 'Contoso.Pipeline.LoggingBehavior<,>'.` |
| Generic parameter or partially closed type passed to `AddOpenBehavior` | Registration | `'TRequest' is a generic type parameter, not a behavior type. Pass a generic type definition, such as typeof(LoggingBehavior<,>).` or `'Contoso.Pipeline.LoggingBehavior<Contoso.Customers.GetCustomer, TResponse>' is partially closed. Pass the generic type definition 'Contoso.Pipeline.LoggingBehavior<,>'.` |
| Value-type open behavior | Registration | `Behavior type 'Contoso.Pipeline.TimingBehavior<,>' is a value type; behaviors must be classes.` |
| Read-only service collection | Registration | `The service collection is read-only, typically because the host has been built. Complete all AddArbitrator calls before building the host.` |
| Invalid closed behavior | Registration | `Type 'Contoso.Pipeline.Auditor' cannot be registered as a behavior: it implements none of IPipelineBehavior<TRequest, TResponse>, IStreamPipelineBehavior<TRequest, TItem>, or INotificationBehavior<TNotification>.` (The same message is used for abstract types and interfaces, with the reason adjusted.) |
| Duplicate behavior | Registration | `Behavior 'Contoso.Pipeline.LoggingBehavior<,>' is already registered at position 1. Each behavior may be registered once; its registration position defines its place in the pipeline.` |
| Behavior would run twice | Registration | `Behavior 'Contoso.Pipeline.LoggingBehavior<Contoso.Customers.GetCustomer, Contoso.Customers.CustomerDto?>' would run twice for request 'Contoso.Customers.GetCustomer': registered as a closed behavior at position 3 and produced by open behavior 'Contoso.Pipeline.LoggingBehavior<,>' at position 1.` |
| Notification behavior would run twice | Registration | `Behavior 'Contoso.Pipeline.TimingBehavior<Contoso.Events.CustomerArchived>' would run twice for notification 'Contoso.Events.CustomerArchived': it is produced by 'Contoso.Pipeline.TimingBehavior<>' at position 1 and by 'Contoso.Pipeline.TimingBehavior<Contoso.Events.CustomerArchived>' at position 2.` |
| Closed behavior with no handler | First `ISender`, `IPublisher`, or `ArbitratorRegistrations` resolution | `Behavior 'Contoso.Customers.AuditDeactivationBehavior' implements IPipelineBehavior<Contoso.Customers.DeactivateCustomer, Arbitrator.Unit>, but none of the types it applies to has a registered handler.` Stream and notification behaviors are reported the same way, naming IStreamPipelineBehavior<…> or INotificationBehavior<…> |
| Change after freezing | Registration | `Arbitrator registrations cannot change once a service provider has attempted to resolve ISender or IPublisher. Complete all AddArbitrator calls before building the service provider.` |
| Registered after the provider was built | First `ISender` or `IPublisher` resolution | `These handlers and behaviors were registered after this service provider was built, so it cannot resolve them: 'Contoso.Customers.LateHandler'. Complete all AddArbitrator calls before building the service provider.` |
| Continuation invoked twice | Dispatch | `Behavior 'Contoso.Pipeline.RetryBehavior<Contoso.Customers.GetCustomer, Contoso.Customers.CustomerDto?>' invoked its continuation more than once while handling 'Contoso.Customers.GetCustomer'. A continuation may be invoked at most once.` |
| Null `Task` | Dispatch | `Handler 'Contoso.Customers.GetCustomerHandler' returned a null Task while handling 'Contoso.Customers.GetCustomer'.` |
| Null stream | First `MoveNextAsync` of a read | `Handler 'Contoso.Customers.SearchCustomersHandler' returned a null stream while handling 'Contoso.Customers.SearchCustomers'.` Behaviors are named the same way |
| Continuation stream read twice | First `MoveNextAsync` of a read | `Behavior 'Contoso.Pipeline.StreamRetryBehavior<Contoso.Customers.SearchCustomers, Contoso.Customers.CustomerDto>' read the stream returned by its continuation more than once while handling 'Contoso.Customers.SearchCustomers'. A continuation's stream may be read at most once.` |
| DI activation failure | Container validation or dispatch | MS.DI's own message, unwrapped, for example `Unable to resolve service for type 'Contoso.Customers.ICustomerRepository' while attempting to activate 'Contoso.Customers.GetCustomerHandler'.` |
| Cancellation | Entry or user code | The standard `OperationCanceledException` message. Its `CancellationToken` is the caller's token when the entry check throws |
| Exceptions from handlers and behaviors | Dispatch | Unchanged. Arbitrator adds no message |
| Notification handler type registered twice | Registration | `Notification handler type 'Contoso.Audit.AuditRecorder' is already registered. Each notification handler type may be registered once across all AddArbitrator calls.` |
| No notification contract | Registration | `Type 'Contoso.Audit.AuditService' cannot be registered as a notification handler: it does not implement INotificationHandler<TNotification>.` |
| Abstract notification type | Registration | `Notification handler 'Contoso.Audit.AuditRecorder' handles 'Contoso.Events.DomainEvent', which is abstract or an interface. Notifications are published by their exact runtime type, so this handler could never run; register a handler for each concrete notification type.` |
| Null notification | Publish | `ArgumentNullException` for parameter `notification` |
| Scanned assembly with unloadable types | Registration | `Assembly 'Contoso.Customers' cannot be scanned because some of its types could not be loaded: Could not load file or assembly 'Contoso.Shared, Version=1.0.0.0'. (and 2 more)` At most five distinct loader messages are listed |
| Scanned handler with unloadable attributes | Registration | `Assembly 'Contoso.Customers' cannot be scanned because the attributes of handler type 'Contoso.Customers.GetCustomerHandler' could not be loaded: Could not load file or assembly 'Contoso.Annotations, Version=1.0.0.0'.` |
| Scanned assembly without handlers | Registration | `Assembly 'Contoso.Customers' contains no request handler or notification handler types. Check that the marker type or assembly passed to AddHandlersFromAssembly is the one that defines the handlers.` |
| Filter excluded every handler | Registration | `The filter excluded all 7 handler types found in assembly 'Contoso.Customers', so nothing was registered.` |

### 7.3 Security considerations

- **No request contents in messages.** Arbitrator's messages contain type names and registration positions only.
  Arbitrator never calls `ToString()` on requests, responses, or stream items, and never includes their values. Record
  types print every property, which could include personal or secret data.
- **Messages are for developers.** Messages name the application's types and namespaces. Applications must not return
  Arbitrator's exception messages to clients; doing so would disclose internal structure, though no data or access.
- **No logging, metrics, or tracing.** Arbitrator emits none of its own. Observability belongs in application
  behaviors, which choose what's safe to record.
- **No string-to-type resolution.** Arbitrator has no object-based dispatch and never resolves a type from a string.
  Untrusted input can't choose a handler type. Dispatch only reaches handler types that the application registered,
  explicitly or through a scan it requested.
- **Isolated service keys.** Private keys keep Arbitrator's descriptors separate from application registrations.
  Arbitrator never overrides or removes descriptors that the application registered.
- **Recursion is the application's responsibility.** Unbounded nested sends can exhaust the stack or memory, and
  Arbitrator doesn't detect cycles (§4.6). Arbitrator adds no other resource-exhaustion surface, since it creates no
  threads, no timers, and no caches that grow with traffic.
- **Streams are pulled and unbuffered.** Arbitrator holds at most one item per read, so a slow reader slows the
  handler instead of growing memory inside Arbitrator. A handler may yield forever: ending the read, through
  cancellation or by stopping, is the caller's responsibility, especially for streams returned to clients.

## 8. Versioning and compatibility

- Versions follow SemVer. Any 0.x release may break the API. Breaking changes are listed in the release notes.
- `src/Arbitrator/PublicAPI.Shipped.txt` lists the public API of the latest published version, and
  `PublicAPI.Unshipped.txt` lists what has changed since. The build fails when the public API differs from the two
  files, so every public change is visible in review. A release moves the unshipped entries into the shipped file
  (`docs/releasing.md`).
- Preview versions may be published while the pinned SDK is itself a preview. A stable version can only be published
  once `global.json` pins a stable SDK, which the release workflow enforces.
