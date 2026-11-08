# Design: Request streaming (v0.4.0)

| | |
| --- | --- |
| Status | Implemented |
| Release | 0.4.0 (published as `0.4.0-preview.1` until .NET 11 is generally available) |
| Date | 2026-11-08 |
| Branch | `feature/request-streaming`, from `main` at `8eeb419` |
| Issue | [#12 Streaming requests](https://github.com/CodeByDylan/Arbitrator/issues/12) |
| Contract reference | [`contract.md`](../contract.md). This document names every section of it that changes |
| Decisions | [26](../decisions/0026-dedicated-streaming-contracts.md), [27](../decisions/0027-stream-through-isender.md), [28](../decisions/0028-stream-components-share-registration.md), [29](../decisions/0029-lazy-re-readable-streams.md), [30](../decisions/0030-linked-stream-cancellation.md), [31](../decisions/0031-continuation-stream-read-once.md) |

## Summary

Arbitrator sends a request to one handler and returns one response. Some requests produce many results over time: search results read from a database page by page, rows exported from a large table, or progress updates from a long operation. Today an application can type such a request as `IRequest<IAsyncEnumerable<T>>`, and it dispatches, but behaviors only wrap the creation of the stream. A logging behavior's "after" runs before the first item is read, and a behavior's `catch` never sees an exception raised while items are produced.

v0.4.0 adds **request streaming**. A stream request implements `IStreamRequest<TItem>`, its single handler implements `IStreamRequestHandler<TRequest, TItem>` and returns `IAsyncEnumerable<TItem>`, and callers read it through `ISender.StreamAsync`. Stream behaviors, `IStreamPipelineBehavior<TRequest, TItem>`, wrap the whole read: they see every item, every exception, and the end of the stream.

Streams are lazy: nothing happens until the caller starts reading, and each read is a complete dispatch. Registration uses the existing builder methods, which recognize stream components from their interfaces. All of Arbitrator's existing rules carry over: exact runtime-type dispatch, the caller's scope, transient components, one global behavior order, a guarded continuation, and exceptions that pass through unwrapped.

## Goals

1. Let a request produce a sequence of items that the caller reads incrementally, with exactly one handler per stream request type.
2. Let behaviors wrap the entire read of a stream: before the first item, around each item, on failure, and after the last item.
3. Keep every existing rule for stream requests: exact-type dispatch, the caller's scope, transient components, strict registration, early validation, unwrapped exceptions, and a single cancellation check.
4. Register stream handlers and behaviors through the existing builder methods and assembly scanning, under the existing behavior order.
5. Honor both the token passed to `StreamAsync` and the token supplied when a read begins.
6. Add no dependencies, and change no existing behavior of requests, notifications or registration.

## Non-goals

- **Streaming notifications.** Notifications stay single, sequential calls.
- **Buffering, batching, back-pressure policies, or parallel reads.** Arbitrator forwards items one at a time, as the caller pulls them.
- **Cancellation checks between items.** The token is checked once, when a read starts (decision 30).
- **Retrying a stream inside the pipeline.** A continuation and its stream may each be used once (decisions 7 and 31).
- **Changing `IRequest<IAsyncEnumerable<T>>`.** It keeps working exactly as today, with behaviors wrapping only the creation of the stream.
- **Non-generic stream requests.** A stream always has an item type.
- **Polymorphic or open-generic stream handlers.** As with other requests, only closed handlers for exact runtime types.
- **Helpers for writing behaviors,** such as base classes that catch exceptions around each item.
- **A source generator or Native AOT support.**

## Current state

- **Contracts.** `IRequest<TResponse>`, `IRequest`, the two request handler interfaces, `IPipelineBehavior<TRequest, TResponse>`, and the `RequestContinuation<TResponse>` delegate. `ISender` has two `SendAsync` overloads.
- **Registration** (`src/Arbitrator/Registration/`).
  - `HandlerTypeInspector` finds every closed `IRequestHandler<,>` on a type and produces a `HandlerContract` (request type, response type, handler type) for each.
  - `RequestTypeInspector` rejects abstract request types and request types with more than one `IRequest<>`.
  - `BehaviorTypeInspector` produces a `ClosedBehaviorRegistration`, holding the `RequestResponseTypes` pairs it implements, or an `OpenBehaviorRegistration`, which applies when `MakeGenericType` over a contract's pair succeeds.
  - `RegisteredPipelines` maps each request type to a `PipelinePlan`: its contract, its planned behaviors in global order, and an invoker template created through `RequestInvoker.CreateTemplate`.
  - `HandlerAssemblyScanner` treats a class as a request handler when it implements `IRequestHandler<,>`.
- **Dispatch** (`src/Arbitrator/Dispatch/`).
  - `DispatchMaps` holds one `FrozenDictionary<Type, RequestInvoker>` for requests and one for notifications. `DispatchTable` is built from them on the first `ISender` or `IPublisher` resolution, and checks that the provider can resolve every component.
  - `Sender.SendAsync` checks for null, looks up the invoker, checks the token once, and awaits `RequestInvoker<TResponse>.InvokeAsync`.
  - `RequestInvoker<TRequest, TResponse>` resolves every behavior and then the handler under the `RequestComponents` service key, and chains them through `BehaviorContinuation` and `HandlerContinuation`, which derive from `GuardedContinuation`. `ComponentTasks` turns a null `Task` into an `InvalidOperationException`.
- **Diagnostics.** `ContractNames` formats `IRequestHandler<…>` and `IPipelineBehavior<…>` for messages. `DispatchErrors` holds the dispatch errors, and `RegistrationErrors` (220 lines) the registration errors.
- **Size.** `ArbitratorBuilder` is 240 lines, against the 250-line type limit.
- **Tests.** 158 TUnit tests run through the public API against MS.DI, using `TestHost`, `Journal`, and `Expect`.

## Proposed solution

Add a stream-shaped twin of the request pipeline and route both through the same registration machinery, distinguished by an internal **pipeline kind**:

1. **Contracts.** Four public types in the `Arbitrator` namespace, and one new `ISender` member.
2. **Registration.** Each handler contract, closed-behavior contract and open behavior records its kind: *response* for the existing contracts, *stream* for the new ones. Inspectors find contracts of both kinds. A behavior applies only to pipelines of its own kind, so request and stream pipelines never mix, while sharing the request-type map, duplicate rules, global behavior order, keyed descriptors, freeze and provider check.
3. **Invokers.** A new internal base, `PipelineInvoker`, replaces the non-generic `RequestInvoker` as the type a `PipelinePlan` creates and the dispatch map holds. `RequestInvoker<TResponse>` and a new `StreamInvoker<TItem>` derive from it. The template factory picks the generic definition from the contract's kind.
4. **Dispatch.** `Sender.StreamAsync` is a C# `async` iterator whose token parameter carries `[EnumeratorCancellation]`. The compiler therefore provides laziness, a fresh state machine for each read, and the combination of both tokens. When a read starts, it checks the request, finds the stream invoker, checks the token once, has the invoker resolve and compose the pipeline, and then yields each item of the outermost component's stream.
5. **Composition.** Stream continuations mirror the request continuations, including the at-most-once guard. In addition, the stream a continuation returns is wrapped so it can be read once (decision 31).

**Why this fits.**
- **One registration path.** Stream handlers are request handlers with a different contract, so they share the map that enforces "one handler per request type", and every registration rule applies without being duplicated.
- **Typed dispatch stays cast-free.** As for `SendAsync`, a registered request type implements exactly one request contract, so the cast from the map entry to `StreamInvoker<TItem>` can't fail. After it, everything is statically typed generic code.
- **The compiler owns the subtle parts.** Using an `async` iterator for `StreamAsync` gives laziness, re-reading, token linking, linked-source disposal, and enumerator disposal on early exit the exact semantics C# developers already know.

## Codebase impact

- **Library (`src/Arbitrator`).**
  - **Public:** four new types and one new `ISender` member. Adding a member to `ISender` breaks types that implement it (decision 27). No other public member changes signature.
  - **Internal:** the pipeline kind, kind-aware inspectors and behavior applicability, a shared invoker base, the stream invoker and its continuations, a single-read stream wrapper, null checks for streams, and new dispatch errors. Several registration error messages are reworded to name both kinds.
  - **Project file:** the version becomes `0.4.0` with suffix `preview.1`.
- **New library or package:** none. Streaming is part of the request contract, depends only on `IAsyncEnumerable<T>` from the base class library, and needs Arbitrator's internals. Decision 18 (a single package) stands.
- **Public API files:** the new types and the `ISender` member are added to `PublicAPI.Unshipped.txt`.
- **Tests:** a new `Streaming/` suite, stream support types, and a stream example.
- **Documentation:** `contract.md`, `architecture.md`, `README.md`, this design, and decisions 26–31.
- **Configuration, infrastructure, CI, persistence, migrations:** none.

## Target structure

Only new and changed paths are listed.

- `src/Arbitrator/`
  - `IStreamRequest{TItem}.cs`: new. The marker interface for stream requests.
  - `IStreamRequestHandler{TRequest,TItem}.cs`: new. The stream handler contract.
  - `IStreamPipelineBehavior{TRequest,TItem}.cs`: new. The stream behavior contract.
  - `StreamContinuation{TItem}.cs`: new. The delegate a stream behavior receives.
  - `ISender.cs`: changed. Adds `StreamAsync`.
  - `ArbitratorBuilder.cs`: changed. XML documentation only: `AddHandler`, `AddBehavior` and `AddOpenBehavior` name the stream contracts. Wording is replaced in place, so the type stays within 250 lines.
  - `Arbitrator.csproj`: changed. The version becomes `0.4.0-preview.1`.
  - `PublicAPI.Unshipped.txt`: changed. The new public API.
  - `Registration/`
    - `PipelineKind.cs`: new. An internal enumeration with the values *Response* and *Stream*.
    - `RequestResponseTypes.cs`: changed. Gains the pipeline kind, which takes part in equality. For a stream contract, the second type is the item type.
    - `HandlerContract.cs`: changed. Exposes the kind through its `Types`.
    - `HandlerTypeInspector.cs`: changed. Finds `IRequestHandler<,>` and `IStreamRequestHandler<,>` contracts.
    - `RequestTypeInspector.cs`: changed. Counts `IRequest<>` and `IStreamRequest<>` contracts together.
    - `BehaviorTypeInspector.cs`: changed. Finds closed contracts of both kinds, and requires an open behavior to implement exactly one pipeline contract of either kind.
    - `OpenBehaviorRegistration.cs`: changed. Records its kind, and doesn't apply to a contract of the other kind, without calling `MakeGenericType`.
    - `HandlerAssemblyScanner.cs`: changed. A request handler contract includes `IStreamRequestHandler<,>`.
    - `PipelinePlan.cs`, `RegisteredPipelines.cs`: changed. Hold and produce `PipelineInvoker` instead of `RequestInvoker`.
    - `RegistrationErrors.cs`: changed. Reworded messages (Detailed behavior §9). If the type would exceed 250 lines, the behavior factories move to a new `BehaviorRegistrationErrors.cs`.
  - `Dispatch/`
    - `PipelineInvoker.cs`: new, replacing `RequestInvoker.cs`. The abstract base: component types, a copy with behaviors, and the template factory, which chooses the request or stream invoker by kind.
    - `RequestInvoker{TResponse}.cs`, `RequestInvoker{TRequest,TResponse}.cs`: changed. Derive from `PipelineInvoker`. The behavior-resolution logic moves to a shared helper so the stream invoker doesn't copy it.
    - `StreamInvoker{TItem}.cs`: new. The abstract stream invoker that `Sender` calls with the request, the scope's provider and the effective token. It returns the outermost component's stream.
    - `StreamInvoker{TRequest,TItem}.cs`: new. Resolves and composes the stream pipeline.
    - `GuardedStreamContinuation{TRequest,TItem}.cs`, `StreamBehaviorContinuation{TRequest,TItem}.cs`, `StreamHandlerContinuation{TRequest,TItem}.cs`: new. Mirror the request continuations. Shared guard logic is extracted rather than copied, where the generic shapes allow it.
    - `SingleReadStream{TItem}.cs`: new. Wraps the stream a continuation returns and allows one `GetAsyncEnumerator` call, which it forwards with the caller's token.
    - `ComponentStreams.cs`: new. Turns a null stream from a handler or behavior into an `InvalidOperationException`, like `ComponentTasks`.
    - `ComponentResolver.cs`: new. Resolves a pipeline's behaviors and handler under the request-components key, shared by both invokers.
    - `DispatchMaps.cs`, `DispatchTable.cs`: changed. One map of `PipelineInvoker` by request type. The table offers a request lookup and a stream lookup, and the missing-handler error names the contract the call expected.
    - `DispatchErrors.cs`: changed. Missing stream handler, null stream, and stream read twice.
    - `Sender.cs`: changed. Adds `StreamAsync`.
  - `Diagnostics/ContractNames.cs`: changed. Formats `IStreamRequestHandler<…>` and `IStreamPipelineBehavior<…>`, chosen by kind.
- `tests/Arbitrator.Tests/`
  - `Streaming/`: new.
    - `StreamDispatchTests.cs`: items, order, identity, struct requests, interface-typed references, typed inference.
    - `StreamLifecycleTests.cs`: laziness, re-reading, early exit, disposal, scopes.
    - `StreamPipelineTests.cs`: behavior order, wrapping, short-circuit, transformation, guards, null streams, kind separation.
    - `StreamCancellationTests.cs`: the entry check, token linking, forwarding.
    - `StreamExceptionTests.cs`: exceptions from handlers and behaviors, before and after items.
    - `StreamRegistrationTests.cs`: inspection, duplicates, ambiguous contracts, open-behavior shape, closed behavior without handler, scanning, container validation.
    - `StreamDiagnosticsTests.cs`: messages.
  - `Support/`: new stream request types, handlers, and a recording stream behavior that writes to `Journal`. A gated stream handler that yields when a `TaskCompletionSource` is released, so tests need no timing.
  - `Examples/`: new `SearchCustomers` stream request, its handler, and a stream logging behavior, run by `ExampleTests`.
- `docs/`
  - `design/request-streaming.md`: new (this document).
  - `decisions/0026`–`0031`: new.
  - `contract.md`, `architecture.md`: changed, as listed under "Rollout and rollback".
- `README.md`: changed, as listed under "Rollout and rollback".

Existing tests are unchanged, except where a registration message they assert on names the contracts it expects. Those tests assert on type names, not exact text (`contract.md` §7.2), so none is expected to change.

## Detailed behavior

### 1. Public API

| Member | Contract |
| --- | --- |
| `IStreamRequest<TItem>` | Marks a request that produces a stream of `TItem` and is handled by exactly one stream handler. Invariant in `TItem` |
| `IStreamRequestHandler<TRequest, TItem>` where `TRequest : IStreamRequest<TItem>` | One method, `HandleAsync(TRequest request, CancellationToken cancellationToken)`, returning `IAsyncEnumerable<TItem>` |
| `IStreamPipelineBehavior<TRequest, TItem>` where `TRequest : IStreamRequest<TItem>` | One method, `HandleAsync(TRequest request, StreamContinuation<TItem> next, CancellationToken cancellationToken)`, returning `IAsyncEnumerable<TItem>` |
| `StreamContinuation<TItem>` | A delegate taking a `CancellationToken` and returning `IAsyncEnumerable<TItem>`: the stream of the rest of the pipeline. May be invoked once, and its stream read once |
| `ISender.StreamAsync<TItem>(IStreamRequest<TItem> request, CancellationToken cancellationToken = default)` | Returns `IAsyncEnumerable<TItem>`, the stream of the outermost component. Never throws |

- **Shape rules.** They match the existing contracts (`contract.md` §3.1): no variance, the constraint is restated by every handler and behavior, the token has a default only on `ISender`, and methods are named `…Async` (decision 1). Decision 2 (`Task`, never `ValueTask`) doesn't apply: streams use `IAsyncEnumerable<T>`, whose `MoveNextAsync` returns `ValueTask<bool>` by the base class library's design.
- **Documentation.** Every type and member has XML documentation stating laziness, the single cancellation check, the at-most-once rules, and the exceptions a read can raise.
- **Annotations.** The new types need none. `AddHandler`, `AddBehavior` and `AddOpenBehavior` keep their existing trimming annotations.
- **Call-site inference.** Each row is verified against the compiler during implementation, then added to `contract.md` §3.3:

  | Call site | Overload chosen | Static result type |
  | --- | --- | --- |
  | `sender.StreamAsync(new SearchCustomers(text))` where `SearchCustomers : IStreamRequest<CustomerDto>` | `StreamAsync`; `TItem` = `CustomerDto` | `IAsyncEnumerable<CustomerDto>` |
  | `IStreamRequest<CustomerDto> request = ...; sender.StreamAsync(request)` | `StreamAsync` | `IAsyncEnumerable<CustomerDto>` |
  | `readonly record struct Countdown : IStreamRequest<int>` | `StreamAsync`; the request is boxed at the call | `IAsyncEnumerable<int>` |
  | `sender.StreamAsync<object>(new SearchCustomers(text))` | Compile error, because `IStreamRequest<>` is invariant | n/a |
  | `sender.SendAsync(new SearchCustomers(text))` | Compile error. A stream request isn't an `IRequest` | n/a |

### 2. Registration

- **Handlers.** `AddHandler<THandler>()` registers every closed `IRequestHandler<,>` and `IStreamRequestHandler<,>` contract `THandler` implements. A class may implement contracts of both kinds and is registered once, with one descriptor. A class implementing only stream contracts is a valid handler. The existing rules apply to every contract: a concrete class, a non-abstract request type, at most one handler per request type, and each handler type registered once.
- **Request types.** A request type implements exactly one of `IRequest`, `IRequest<TResponse>` and `IStreamRequest<TItem>`, counted together. A handler for a request type that implements more than one is rejected with the existing "more than one request contract" error, which lists every contract found.
- **Closed behaviors.** `AddBehavior<TBehavior>()` accepts a class implementing any number of `IPipelineBehavior<,>` and `IStreamPipelineBehavior<,>` contracts. It applies to every pipeline whose kind, request type and response or item type match one of them exactly.
- **Open behaviors.** `AddOpenBehavior(type)` accepts a generic type definition with exactly two type parameters that implements exactly one pipeline contract, of either kind, using those parameters in order. A definition implementing both kinds is rejected with the reworded "invalid open behavior" error. An open behavior applies only to pipelines of its own kind, and then exactly as today: when closing it over the contract's types passes the runtime's constraint check.
- **Order.** Behaviors of both kinds share one global registration order and one sequence of positions. Within a pipeline, only behaviors of its kind appear, in that order. "Already registered at position N" counts behaviors of both kinds.
- **Closed behavior without handler.** A closed stream behavior that matches no registered stream contract fails the first `ISender` or `IPublisher` resolution, as closed request behaviors do.
- **Duplicates and conflicts.** Every duplicate rule in `contract.md` §5.4 applies unchanged, including "behavior would run twice".
- **Descriptors.** Stream handlers and stream behaviors are registered as transient, keyed descriptors of their implementation types, under the request-components key. Container validation and the "registered after the provider was built" check therefore cover them with no new mechanism.
- **Assembly scanning.** A class implementing `IStreamRequestHandler<,>` is a request handler candidate. It is registered in the request handler phase, in full-name order. Behaviors, including stream behaviors, are never registered by a scan. The "no handler types" message is unchanged, because stream handlers are request handlers.

### 3. Reading a stream

Calling `StreamAsync` only creates the stream object. It validates nothing, resolves nothing, inspects no token and never throws (decision 29).

Each read of that object is a separate dispatch. A read begins with `GetAsyncEnumerator`, called directly or by `await foreach`, and its steps run on the first `MoveNextAsync`:

1. **Effective token.** It's the `StreamAsync` token, unless that is the default token, in which case it's the read's token. When both are non-default and differ, it's a linked token, created for the read and disposed when the read ends (decision 30). `GetAsyncEnumerator` does nothing else.
2. **Null request** → `ArgumentNullException` for parameter `request`. Nothing is resolved.
3. **Missing handler.** No stream handler for `request.GetType()` → `ArbitratorConfigurationException` naming the request type and `IStreamRequestHandler<TRequest, TItem>`, with the base-type hint when a base type of the request is registered. Nothing is resolved.
4. **Cancellation check.** The effective token is already canceled → `OperationCanceledException` carrying the effective token. Nothing is resolved.
5. **Resolution.** Every applicable stream behavior is resolved, outermost first, then the handler, from the scope that resolved the `ISender`. DI exceptions propagate unwrapped, before any component runs (decision 13).
6. **Invocation.** The outermost component's `HandleAsync` is called with the request and the effective token. A null return → `InvalidOperationException` naming the component and the request type.
7. **Reading.** Arbitrator starts reading the returned stream, passing the effective token to its `GetAsyncEnumerator`, and yields each item to the caller as it arrives, in order and unchanged. It holds at most one item at a time.
8. **End.** When the outermost stream ends, the caller's `MoveNextAsync` returns false. Arbitrator disposes the enumerator it obtained in step 7, and then the linked token source, if any.

All of these failures arrive from a `MoveNextAsync` call, never synchronously from `StreamAsync` or `GetAsyncEnumerator`. Struct requests are boxed when passed to `StreamAsync`, and each component receives a copy.

### 4. The stream pipeline

- **Composition.** The outermost behavior receives a continuation that invokes the next behavior, and so on, and the innermost behavior's continuation invokes the handler. With no behaviors, the handler is called directly and no continuations are allocated.
- **Behaviors wrap the read.** A behavior typically returns its own `async` iterator that reads `next(token)` and yields each item. Code before its loop runs before the first item is requested from the inner pipeline; code after it runs after the inner stream ends; `finally` blocks run on completion, failure, and early exit.
- **What a behavior may do.** Arbitrator imposes nothing on the items. A behavior may pass items through, transform, filter, or add items, observe exceptions, or substitute the token it passes to `next`, under the same rules as row 20 of the behavioral contract.
- **Short-circuit.** A behavior that never invokes `next` produces the stream by itself. Inner behaviors and the handler aren't invoked, although they were already constructed. Returning an empty stream is allowed.
- **Continuation guard.** Invoking `next` a second time throws `InvalidOperationException` synchronously from `next`, with the existing message (decision 7).
- **Single-read guard.** The stream `next` returns may be read once. A second `GetAsyncEnumerator` call on it throws `InvalidOperationException` naming the behavior and the request type (decision 31). The guard forwards the first call, with the token it was given, to the inner component's stream, and adds no work per item.
- **Null streams.** A behavior or handler reached through a continuation that returns a null stream causes `next` to throw `InvalidOperationException` naming that component and the request type.
- **Not checked.** Null items are returned as they are. A component whose stream returns a null enumerator causes the runtime's `NullReferenceException`, which propagates unchanged.
- **Catching while yielding.** C# rejects `yield return` inside a `try` block that has a `catch` clause (CS1626). A behavior that translates exceptions per item reads the inner enumerator by hand. The README shows this pattern.

### 5. Cancellation

- **One check per read.** The effective token is checked once per read, after the handler lookup (step 4). After that it is only forwarded: to the outermost component's `HandleAsync`, and to its stream's `GetAsyncEnumerator`. Arbitrator never checks it between items or components (decision 14).
- **During the read.** If code throws `OperationCanceledException`, it propagates unchanged and ends the read. If the code ignores the token, items keep flowing. Arbitrator doesn't promise prompt cancellation.
- **Token linking.** The token components receive, and an entry `OperationCanceledException` carries, is the caller's token when only one token was supplied, or when both are the same token. It is the linked token when the two differ.
- **Linked source lifetime.** The linked source is disposed when the read ends, including on early exit and failure. A component that keeps the token beyond the read gets a token whose source is disposed, which is the same rule as row 20.

### 6. Early exit and disposal

- **Stopping early.** When the caller stops reading, through `break`, an exception in its loop body, or by disposing the enumerator, Arbitrator disposes the outermost component's enumerator. A behavior using `await foreach` disposes the inner enumerator in turn, so every component's `finally` blocks run, innermost first, as with an exception. No further items are requested.
- **Disposal of services.** Arbitrator never disposes handlers, behaviors or any other service. The scope disposes them when it ends (`contract.md` §5.3). Principle 4 is reworded to say Arbitrator never disposes a service; the only things it disposes are the enumerator it obtains while reading and the linked token source it creates.
- **Abandoned enumerators.** An enumerator that is neither read to the end nor disposed keeps its components and their scoped dependencies referenced until it is collected. `await foreach` always disposes, so this happens only with manual enumeration.

### 7. Scopes and threading

- **Scope.** Components are resolved from the scope that resolved the `ISender`, when the read starts. The read must happen inside that scope's lifetime. A stream returned out of a `using` block that disposes the scope fails on its first `MoveNextAsync` with DI's `ObjectDisposedException`. A scope disposed during a read leaves components with disposed scoped dependencies, which is not guarded. In ASP.NET Core, returning the stream from an endpoint reads it during the response, inside the request's scope.
- **Context.** Arbitrator's own awaits use `ConfigureAwait(false)`. Arbitrator calls each component synchronously from the caller's `MoveNextAsync`, before awaiting anything, so each step of the outermost component starts on the context from which the caller requested the item.
- **Concurrency.**
  - One stream object may be read by several readers at once. Each read is a separate dispatch in the same scope, with the same caveats as concurrent sends in one scope (`contract.md` §4.5).
  - A single enumerator isn't safe for concurrent `MoveNextAsync` calls, as for every `IAsyncEnumerator<T>`.
  - Arbitrator's own state is immutable once frozen, and each read has its own continuations and guards.
- **Nested dispatch.** Components may send, publish, or stream again from inside a read, under the rules of `contract.md` §4.6.

### 8. Edge cases

| Case | Outcome |
| --- | --- |
| A stream request type that derives from a handled stream request type | Treated as a missing handler. The message names the registered base type |
| A request type implementing `IRequest<X>` and `IStreamRequest<Y>` | Registering its handler fails with the "more than one request contract" error |
| `IRequest<IAsyncEnumerable<T>>` | Unchanged: a normal request, with request behaviors wrapping only the creation |
| A handler class implementing both `IRequestHandler<A, R>` and `IStreamRequestHandler<B, I>` | One registration. `SendAsync(new A())` and `StreamAsync(new B())` each reach it, with a new instance for every dispatch |
| A closed behavior implementing `IPipelineBehavior<A, R>` and `IStreamPipelineBehavior<B, I>` | One registration, applying to both pipelines at the same position |
| An open behavior definition implementing both pipeline interfaces | Rejected at registration. Its constraints could never be satisfied together |
| A handler whose stream yields no items | The caller's first `MoveNextAsync` returns false. Behaviors still run their code around the loop |
| A handler that throws after yielding items | The items already yielded stay delivered. The same exception instance reaches each enclosing behavior's loop, then the caller's `MoveNextAsync` |
| A handler that throws from `HandleAsync` before returning a stream (not an iterator) | The exception reaches the enclosing behavior's `next` call, or the caller's first `MoveNextAsync` when there are no behaviors |
| The caller never reads the stream | Nothing is validated, resolved or run |
| The caller reads the same stream object twice | Two dispatches, two sets of components, and the handler runs twice |
| A behavior returns the stream from `next` without wrapping it | Allowed. The behavior has no code around the read |
| `StreamAsync` called with a token that is already canceled, and never read | Nothing happens |

### 9. Diagnostics

New and changed messages in `contract.md` §7.2. The wording is representative, and tests assert on the exception type and the type names a message contains.

| Failure | Stage | Representative message |
| --- | --- | --- |
| Missing stream handler (new) | Read | `No handler is registered for stream request type 'Contoso.Customers.SearchCustomers'. Register a class implementing IStreamRequestHandler<Contoso.Customers.SearchCustomers, Contoso.Customers.CustomerDto> with AddHandler<THandler>(). Handlers are matched to the exact runtime type of the request.` The base-type sentence is appended as for requests |
| Null stream from a handler (new) | Read | `Handler 'Contoso.Customers.SearchCustomersHandler' returned a null stream while handling 'Contoso.Customers.SearchCustomers'.` |
| Null stream from a behavior (new) | Read | `Behavior 'Contoso.Pipeline.StreamLoggingBehavior<Contoso.Customers.SearchCustomers, Contoso.Customers.CustomerDto>' returned a null stream while handling 'Contoso.Customers.SearchCustomers'.` |
| Continuation stream read twice (new) | Read | `Behavior 'Contoso.Pipeline.StreamRetryBehavior<Contoso.Customers.SearchCustomers, Contoso.Customers.CustomerDto>' read the stream returned by its continuation more than once while handling 'Contoso.Customers.SearchCustomers'. A continuation's stream may be read at most once.` |
| Handler with no contract (changed) | Registration | `Type 'Contoso.Customers.CustomerService' cannot be registered as a handler: it implements none of IRequestHandler<TRequest, TResponse>, IRequestHandler<TRequest>, or IStreamRequestHandler<TRequest, TItem>.` |
| Request with several request contracts (changed) | Registration | `Request type 'Contoso.Customers.Ambiguous' implements more than one request contract (IRequest<A>, IStreamRequest<B>). A request type must implement exactly one of IRequest, IRequest<TResponse>, or IStreamRequest<TItem>.` |
| Invalid open behavior (changed) | Registration | `Open behavior 'Contoso.Pipeline.AuditBehavior<T>' must be a generic type definition with exactly two type parameters that implements exactly one of IPipelineBehavior<TRequest, TResponse> or IStreamPipelineBehavior<TRequest, TItem>, using those parameters in order.` |
| Invalid closed behavior (changed) | Registration | `Type 'Contoso.Pipeline.Auditor' cannot be registered as a behavior: it implements neither IPipelineBehavior<TRequest, TResponse> nor IStreamPipelineBehavior<TRequest, TItem>.` |
| Closed stream behavior with no handler (changed) | First `ISender` or `IPublisher` resolution | `Behavior 'Contoso.Customers.AuditSearchBehavior' implements IStreamPipelineBehavior<Contoso.Customers.SearchCustomers, Contoso.Customers.CustomerDto>, but no handler is registered for 'Contoso.Customers.SearchCustomers'.` |

The "Read" stage is new in `contract.md` §5.5 and §7.2: it is dispatch for streams, and it happens on the first `MoveNextAsync` of a read. Messages never include items or request values, and Arbitrator never calls `ToString()` on them.

### 10. Behavioral contract additions (`contract.md` §6)

New rows 44 to 56:

- **44 Stream request.** Each item the handler yields reaches the caller in order, as the same instance, once. The handler receives the caller's request instance and the effective token. Nothing is buffered.
- **45 Laziness.** `StreamAsync` never throws and does nothing. Every failure in rows 46–56 arrives from a `MoveNextAsync` call.
- **46 Each read is a dispatch.** Reading the same stream object again resolves new components and runs the pipeline again.
- **47 Missing stream handler,** including for derived types: `ArbitratorConfigurationException` at the first `MoveNextAsync`, before the cancellation check. Nothing is resolved.
- **48 Token at the start of a read.** The effective token (decision 30) is checked once, after the handler lookup. If it is canceled, the read is canceled with an `OperationCanceledException` carrying it, and nothing is resolved.
- **49 Cancellation during a read.** Forwarded only, never checked between items.
- **50 Stream behaviors.** Applicable stream behaviors run in global registration order, outermost first, and wrap the entire read. Request behaviors never wrap stream handlers, and stream behaviors never wrap request handlers.
- **51 Short-circuit and replacement.** A stream behavior may produce items without invoking `next`. Inner components aren't invoked, although they were constructed. Its stream is the result.
- **52 Null stream** from a handler or behavior: `InvalidOperationException` naming the component and the request type.
- **53 Continuation reuse.** Invoking `next` twice, or reading its stream twice, throws `InvalidOperationException`. The inner components run once.
- **54 Exceptions during a read.** The same exception instance reaches each enclosing behavior and the caller. Items already yielded stay delivered.
- **55 Early exit.** When the caller stops reading, the outermost component's enumerator is disposed and no further items are requested. Arbitrator disposes no services.
- **56 Stream activation failure.** DI's exception propagates unwrapped from the first `MoveNextAsync`, before any component runs.

Rows 1–43 are unchanged. Rows 36 and 39 implicitly cover stream handlers, because they are request handlers, and §4.4 says so explicitly.

## Dependencies

No dependency is added, updated, replaced or removed. `IAsyncEnumerable<T>`, `IAsyncEnumerator<T>`, `EnumeratorCancellationAttribute` and `CancellationTokenSource` are part of the .NET 11 base class library. The `System.Linq.AsyncEnumerable` operators aren't needed by the library or the tests.

| Dependency | Change | Audit result |
| --- | --- | --- |
| None | n/a | `dotnet list package --vulnerable --include-transitive` against nuget.org on 2026-11-08 reported no vulnerable packages in `Arbitrator` or `Arbitrator.Tests` |

## Security considerations

- **No new trust boundary.** Streams are dispatched by the runtime type of a request object that the application constructed, exactly like `SendAsync`. There is no string-to-type resolution, no object-based dispatch, and no handler is reachable unless the application registered it.
- **Item contents.** Arbitrator never inspects, logs, formats or calls `ToString()` on items or requests. Messages contain type names only.
- **Resource use.** Arbitrator holds at most one item per read and buffers nothing. Reads are pulled by the caller, so a slow reader slows the handler instead of growing memory inside Arbitrator. Each read allocates its continuations, guards and, when two tokens differ, one linked token source, all released when the read ends.
- **Unbounded streams.** A handler may yield forever. Ending the read is the caller's responsibility, through cancellation or by stopping, and the README says so for streams returned to clients, for example from HTTP endpoints. Arbitrator adds no limits, consistent with forwarding cancellation without interpreting it.
- **Scope lifetime.** A read that outlives its scope fails on DI's `ObjectDisposedException` or a disposed dependency. It never falls back to the root provider. The README warns against returning a stream out of the scope that created it.
- **Exceptions.** Exceptions from components pass through unchanged, as today. Applications must not return Arbitrator's messages to clients (`contract.md` §7.3).
- **No new attack surface.** No network, file access, deserialization, threads, timers, or static mutable state is added.

## Testing strategy

All tests are TUnit tests through the public API against MS.DI, using `TestHost`, `Journal` and `Expect`. No test relies on sleeps or wall-clock timing: coordination uses `TaskCompletionSource`, and gated handlers yield only when a test releases them. Each new contract row has at least one test.

- **Dispatch.** Items arrive in order and as the same instances. The handler receives the caller's request instance. Struct requests, interface-typed references, and a handler implementing request and stream contracts both work. `StaticType` checks the inferred `IAsyncEnumerable<TItem>` for the compilable inference rows.
- **Laziness and re-reading.** Calling `StreamAsync` with a null request, an unregistered type, or a canceled token doesn't throw, and the journal stays empty until reading starts. A second read journals a second, complete dispatch with new instances.
- **Pipeline.** Order across interleaved open and closed, request and stream behaviors registered over two `AddArbitrator` calls. Constrained open stream behaviors that don't apply are left out. A request behavior doesn't wrap a stream handler, and the reverse. Before, per-item and after journal entries surround the handler's items. Short-circuit, transformation, filtering, and token substitution.
- **Guards.** `next` invoked twice. The continuation's stream read twice, with the inner handler run once. Null streams from a handler, an inner behavior and the outermost behavior.
- **Cancellation.** A canceled `StreamAsync` token, a canceled read token, and both. Components receive the single supplied token, or a linked token that is canceled when either source is. The linked source is disposed when the read ends: a linked token captured by the handler is no longer canceled when a source is canceled after the read. No check between items: a handler that ignores a token canceled after the first item still delivers every item.
- **Exceptions.** A handler throwing before any item, after some items, and from a non-iterator `HandleAsync`. A behavior catching and translating exceptions by reading the enumerator by hand. Same-instance assertions throughout.
- **Early exit.** `break` after the first item disposes the pipeline: each behavior's `finally` and the handler's `finally` run in the expected order, and the handler isn't asked for another item.
- **Scopes.** Components come from the reading scope. Reading after the scope is disposed fails with `ObjectDisposedException`. Concurrent reads of one stream object get distinct component instances.
- **Registration.** Stream handler registration, a type implementing both kinds, duplicate request types across kinds, the ambiguous-contract error, abstract stream request types, the open-behavior shape with both interfaces, a closed stream behavior without handler at first resolution, duplicate stream behaviors, "would run twice", the frozen and read-only errors, and copied collections.
- **Scanning.** A scan registers stream handlers, in the request-handler phase, and never stream behaviors.
- **Container validation.** `ValidateOnBuild` reports a missing dependency of a stream handler and of an applicable stream behavior.
- **Diagnostics.** Every new and changed message names the expected types.
- **Example.** The README's streaming example compiles and runs in `Examples/`.
- **Regression.** The existing 158 tests pass unchanged.
- **Analyzers.** The Release build has zero warnings, including trimming, AOT and public API analyzers, with no new suppressions.
- **Performance tests:** not needed. A read adds a dictionary lookup, the continuations, and one extra `MoveNextAsync` frame per item for Arbitrator's iterator. This is recorded as accepted overhead.

## Rollout and rollback

- **Release.** The work merges to `main` through a pull request that must pass "Build and test". It ships as `0.4.0-preview.1` through `docs/releasing.md`. The release notes state that streaming is additive, except that classes implementing `ISender` must add `StreamAsync`.
- **Documentation updates in the same change.**
  - `contract.md`:
    - §1 principle 1: "one pipeline abstraction for each kind of request". Principle 4: Arbitrator never disposes a service, and disposes only the enumerator it reads and the token source it links.
    - §3.1 lists the four new types, and the shape rules name them. "One request contract per request type" includes `IStreamRequest<TItem>`.
    - §3.2: `AddHandler`, `AddBehavior`, `AddOpenBehavior` and scanning accept stream components.
    - §3.3 gains the verified inference rows.
    - §4.2: a behavior applies only to pipelines of its own kind.
    - §4.4: stream handlers are request handler candidates.
    - A new §4.8, "Streaming requests", summarizes Detailed behavior §3–§7.
    - §5.1: stream handlers and behaviors appear with the existing handler and behavior rows.
    - §5.3: enumerator and linked-source disposal.
    - §5.5: a "Read" stage alongside "Dispatch".
    - §6 gains rows 44–56.
    - §7.1: `InvalidOperationException` also covers null streams and a continuation's stream read twice.
    - §7.2 gains the new and changed messages.
    - §7.3: no buffering, and unbounded streams are the caller's to end.
  - `architecture.md`: the source layout mentions streams; registration explains pipeline kinds; the dispatch table holds `PipelineInvoker` entries; a new "Reading a stream" section describes the iterator, composition, guards and token linking.
  - `README.md`: a new "Streaming requests" section after "Notifications" covers a stream request, its handler, `await foreach`, `WithCancellation`, a stream behavior, the hand-written enumerator pattern for catching, laziness, re-reading, and the scope warning. "The contract in brief" gains rows for laziness, stream behaviors and stream cancellation.
  - `docs/design/request-streaming.md`: the status becomes "Implemented". Issue #12 is closed.
- **Migration.** None for applications that use `ISender`. Classes that implement `ISender` add `StreamAsync`. Applications using `IRequest<IAsyncEnumerable<T>>` may move to stream requests to get behaviors around the read.
- **Feature flag:** none needed. Streaming runs only when `StreamAsync` is called.
- **Rollback.** Consumers pin `0.3.0-preview.1`. Published versions can't be removed. A defective preview is unlisted and replaced by a higher version.

## Risks and mitigations

| Risk | Cause | Impact | Detection | Mitigation |
| --- | --- | --- | --- | --- |
| Breaking `ISender` implementers | A new interface member | Test fakes and decorators stop compiling | Compile errors in consumers | Pre-1.0 versioning, release notes, and decision 27 |
| A read outlives its scope | Lazy streams returned out of a `using` scope | `ObjectDisposedException`, or failures from disposed scoped services mid-stream | Tests in the consuming application | Documented in the contract and README, with a test for the first-read failure |
| Behaviors that can't catch while yielding | CS1626 | Authors give up on error translation or write incorrect code | Code review | The README shows the hand-written enumerator pattern, and an example test runs it |
| Surprise that reading twice runs the handler twice | Lazy, re-readable streams | Duplicate side effects | Tests in the consuming application | Decision 29, the contract row, and XML documentation on `StreamAsync` |
| A leaked linked token source or enumerator | Early exit or failure paths | Memory held until collection | Early-exit and failure tests observe disposal | The compiler-generated iterator disposes both in every path, and tests cover break, exception and manual disposal |
| Request and stream pipelines mixing | Kind not checked in applicability | A behavior applied to the wrong pipeline, or a failed cast at dispatch | Kind-separation tests | The kind takes part in contract equality, and open behaviors check it before `MakeGenericType` |
| The cast to `StreamInvoker<TItem>` failing | A request type registered with contracts of both kinds | `InvalidCastException` at dispatch | Ambiguous-contract test | Registration rejects request types with more than one request contract of any kind |
| Duplicated pipeline code | Two invoker and continuation families | Divergent fixes | Review against the dotnet-11 rule that forbids copy-paste | Shared resolver and guard logic are extracted. The families differ only in the return type |
| Type size limits | `ArbitratorBuilder` at 240 and `RegistrationErrors` at 220 lines | A maintainability standard is breached | Measuring during implementation | Builder documentation is reworded in place. Behavior errors move to `BehaviorRegistrationErrors` if needed |
| Per-item overhead | Arbitrator's iterator adds a frame to each `MoveNextAsync` | Slightly slower streams | Not measured | Accepted. Behaviors aren't wrapped per item, and the single-read guard acts only on `GetAsyncEnumerator` |

## Pros and cons

**Pros**

- Behaviors finally see the whole read: timing, item counts, failures and completion.
- One sender, one set of builder methods, one behavior order, and one registration path for every request.
- Laziness, re-reading and token linking follow the C# iterator semantics developers already know.
- Every existing rule and diagnostic carries over, and the "one handler per request type" check spans both kinds.
- No new dependency or package.

**Cons**

- Four new public types, and a second pipeline abstraction for authors to learn.
- A breaking change for classes implementing `ISender`.
- Failures surface where the stream is read, which may be far from where it was created.
- Behaviors that catch exceptions per item need a hand-written enumerator.
- Two parallel invoker and continuation families to maintain.

**Alternatives considered**

- **Keeping `IRequest<IAsyncEnumerable<T>>`.** Rejected, because behaviors can't observe the read (decision 26).
- **Special-casing `IAsyncEnumerable<T>` responses in `IPipelineBehavior`.** Rejected: one interface with two meanings.
- **`CreateStream`, or a separate `IStreamSender`.** Rejected in decision 27.
- **Separate builder methods, or a separate behavior order.** Rejected in decision 28.
- **Eager validation in `StreamAsync`, or single-use streams.** Rejected in decision 29.
- **Ignoring the read's token.** Rejected in decision 30.
- **Not guarding the continuation's stream.** Rejected in decision 31.
- **Wrapping each item to check cancellation or nulls.** Rejected: per-item cost, contrary to decision 14 and to the "not checked" rule for null responses (row 27).

## Acceptance criteria

Each criterion has at least one test in `tests/Arbitrator.Tests/Streaming/`, unless it names another place.

- **ST1** `StreamAsync` delivers every item a registered stream handler yields, in order and as the same instances. The handler receives the caller's request instance.
- **ST2** `StreamAsync` with a null request, an unregistered request type, or a canceled token returns without throwing and runs nothing. The failure arrives from the first `MoveNextAsync`: `ArgumentNullException` for `request`, `ArbitratorConfigurationException` naming `IStreamRequestHandler<…>` (with the base-type hint for derived types), or `OperationCanceledException` carrying the effective token.
- **ST3** Reading one stream object twice performs two complete dispatches with distinct component instances.
- **ST4** `AddHandler` registers stream contracts. A class implementing request and stream contracts is registered once and reachable through both `SendAsync` and `StreamAsync`.
- **ST5** A request type implementing more than one of `IRequest`, `IRequest<T>` and `IStreamRequest<T>` is rejected when its handler is registered. A second handler for a stream request type, or for any request type across kinds, is rejected.
- **ST6** Closed and open stream behaviors registered with `AddBehavior` and `AddOpenBehavior` wrap stream handlers in global registration order, interleaved with request behaviors in position numbering. Request behaviors never wrap stream handlers, and stream behaviors never wrap request handlers.
- **ST7** An open behavior definition implementing both pipeline interfaces is rejected with the reworded invalid-open-behavior error. A closed stream behavior with no matching handler fails the first `ISender` resolution.
- **ST8** A stream behavior observes code before the first item, each item, and code after the last item, in that order around the handler's items.
- **ST9** A short-circuiting stream behavior's items are the result, and the handler isn't invoked.
- **ST10** Invoking `next` twice, or reading its stream twice, throws `InvalidOperationException`, and the handler runs once.
- **ST11** A null stream from a handler or a behavior throws `InvalidOperationException` naming it and the request type.
- **ST12** Components receive the single supplied token, or a linked token when the `StreamAsync` and read tokens differ. A canceled effective token at the start of a read cancels it before resolution. No check happens between items.
- **ST13** An exception thrown by the handler before or after items reaches each enclosing behavior and the caller as the same instance. Items yielded before it stay delivered.
- **ST14** Stopping a read early disposes the pipeline's enumerators, runs every component's `finally` blocks, innermost first, and requests no further item. The linked token source is disposed when the read ends.
- **ST15** Components are resolved from the reading scope. A read after the scope is disposed fails with `ObjectDisposedException` from the first `MoveNextAsync`.
- **ST16** DI activation failures propagate unwrapped from the first `MoveNextAsync`, before any component runs.
- **ST17** Assembly scanning registers stream handlers and never stream behaviors.
- **ST18** Container validation covers stream handlers and applicable stream behaviors.
- **ST19** Every compilable call-site inference row has a `StaticType` check, and the rows are added to `contract.md` §3.3.
- **ST20** The streaming example in `README.md` compiles and runs in `tests/Arbitrator.Tests/Examples/`.
- **ST21** The existing test suite passes unchanged. The Release build has zero warnings with no new suppressions, and the public API files list exactly the new members.
- **ST22** `contract.md`, `architecture.md` and `README.md` contain the updates listed under "Rollout and rollback". The package version is `0.4.0-preview.1`.
