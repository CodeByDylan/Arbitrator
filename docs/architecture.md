# Arbitrator architecture

This document explains how Arbitrator is built, for contributors. It isn't normative: everything here may change in
any release, provided [`contract.md`](contract.md) still holds. Tests exercise only the public contract. Type names
are given so the code is easy to find; the XML documentation on each type is the detailed reference.

## Source layout

| Folder and namespace | Contents |
| --- | --- |
| `src/Arbitrator/`, `Arbitrator` | The public contracts, `ArbitratorBuilder`, and `ArbitratorConfigurationException` |
| `src/Arbitrator/`, `Microsoft.Extensions.DependencyInjection` | `ArbitratorServiceCollectionExtensions`, the `AddArbitrator` entry point. The only type whose namespace doesn't match its folder |
| `Registration/`, `Arbitrator.Registration` | The registry, type inspectors, assembly scanning, descriptor emission, and registration errors |
| `Dispatch/`, `Arbitrator.Dispatch` | `Sender`, `Publisher`, the pipeline kinds, the dispatch table, invokers, guarded continuations, the service keys, and dispatch errors |
| `Diagnostics/`, `Arbitrator.Diagnostics` | Formatting of type and contract names for messages |

There is no `InternalsVisibleTo`: tests use only the public API.

## Registration

1. **The first `AddArbitrator` call on a service collection** adds five descriptors: the `ArbitratorRegistry`
   instance as a singleton, the `DispatchTable` as a singleton built by a factory from the registry, the public
   `ArbitratorRegistrations` as a singleton taken from the dispatch table, and the scoped `ISender` and `IPublisher`
   implementations.
2. **Later calls** find the registry by its descriptor and add to it. A registry found in a collection it doesn't own
   came from a copied collection, so it is copied and the copy replaces it.
3. **Each builder call** validates the type, records it in the registry's `Registrations`, and adds keyed transient
   descriptors. Request components, notification handlers, and notification behaviors each use their own key
   (`ArbitratorServiceKey`). Handlers and closed behaviors are registered as their implementation type. An open
   behavior is registered as one closed instantiation per applicable handler contract or notification type.
4. **Pipeline kinds.** Every handler contract, closed-behavior contract, and open behavior records its `PipelineKind`:
   `Response` for the `IRequest<>` contracts and `Stream` for the `IStreamRequest<>` contracts. The kind holds the
   generic definitions of its request, handler, and behavior contracts, so the inspectors find both kinds in one pass,
   and a behavior applies only to contracts of its own kind. Both kinds share the request-type map, so "one handler
   per request type" spans them. Notification behaviors are a third kind outside `PipelineKind`: a closed registration
   lists the notification types it wraps, and open ones are `OpenNotificationBehaviorRegistration`. Both open kinds
   derive from the `closed` record `OpenRegistration`, so every `switch` over registrations must handle each kind.
5. **Plans.** `PipelinePlan` (one per request type) and `NotificationPlan` (one per notification type) both implement
   `IBehaviorPlan`, so placement, the "would run twice" check, and descriptor creation serve both. Their shared list
   logic lives in `PlannedBehaviors`. `RegisteredBehaviors` plans a new plan of either kind from the behaviors
   registered so far.
6. **Emission is incremental.** A new handler contract or notification type gets descriptors for every open behavior
   already registered that applies to it, and a new open behavior gets one for every applicable contract or
   notification type already registered. That's why the relative order of handler and behavior calls never matters.
   Applicability is computed once per (behavior, contract) pair: `MakeGenericType` succeeding means the behavior
   applies, and a constraint violation means it doesn't.
7. **Scanning.** `HandlerAssemblyScanner` finds, sorts, and filters candidates outside the registry lock, because the
   filter is caller code. `ArbitratorRegistry` then registers the result into a copy of its `Registrations` under the
   lock, using the same per-type logic as explicit registration, and replaces its registrations with the copy only on
   success.

Arbitrator never builds a service provider during registration.

## Building the dispatch table

The `DispatchTable` singleton is created the first time a provider resolves `ISender` or `IPublisher`:

1. The registry is frozen. Any later builder call throws.
2. Every closed behavior must apply to at least one registered handler contract or notification type with handlers.
   Because the singleton factory throws otherwise, every later resolution fails the same way.
3. `Registrations` produces `DispatchMaps`: one `PipelineInvoker` per handler contract, holding the handler type and
   the applicable behavior types in registration order, and one `NotificationInvoker<TNotification>` per notification
   type, holding its ordered handler types and the behavior types that wrap each. A pipeline invoker is a
   `RequestInvoker<TRequest, TResponse>` or a `StreamInvoker<TRequest, TItem>`, chosen by the contract's kind. Both
   maps are `FrozenDictionary` instances keyed by the request or notification type. In the same call, each pipeline
   and notification plan describes itself, and the descriptions become the public `ArbitratorRegistrations`, which the
   dispatch table exposes. Because both come from the same frozen plans, the description always matches what dispatch
   runs.
4. Through `IServiceProviderIsKeyedService`, the table checks that the provider can resolve every type the invokers
   refer to. A type is missing when it was registered after the provider was built, because MS.DI doesn't make a
   collection read-only when it builds a provider.

## Sending a request

```text
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

**From the runtime type to typed code.** `Sender.SendAsync<TResponse>` looks up `request.GetType()` and casts the
entry to `RequestInvoker<TResponse>`. The cast can't fail: registration guarantees that a request type implements
exactly one request contract of any kind, and since `IRequest<>` is invariant the call site's `TResponse` must be that
interface's argument. Inside `RequestInvoker<TRequest, TResponse>`, `(TRequest)request` is the only other cast; it
unboxes struct requests. Everything after it is statically typed generic code, so a dispatch costs one dictionary
lookup, one virtual call, and two casts, with no `MethodInfo.Invoke` and therefore no `TargetInvocationException`.

**Composition.** Each behavior is wrapped in a `BehaviorContinuation`, and the handler in a `HandlerContinuation`.
Both derive from `GuardedContinuation<TRequest, TResult>`, whose `InvokeAsync` method becomes the
`RequestContinuation<TResponse>` a behavior receives. `ComponentResolver` resolves the components of both invoker
kinds. It sets an "invoked" flag with `Interlocked.Exchange`, and turns a null `Task` from its component into an
`InvalidOperationException`. With zero behaviors, the handler is called directly and no continuations are allocated.

**Awaiting.** Internal awaits use `ConfigureAwait(false)`. Arbitrator never awaits before calling into user code, so a
behavior or handler starts on the caller's context. Notification publishing is the one exception (contract §4.3).

## Reading a stream

`Sender.StreamAsync` is a C# `async` iterator whose token parameter carries `[EnumeratorCancellation]`. The compiler
therefore provides what the contract promises about streams (§4.8): nothing runs until the first `MoveNextAsync`, every
`GetAsyncEnumerator` call gets a fresh state machine and so a fresh dispatch, the two tokens are combined into a linked
token when they differ, and the linked source and the inner enumerator are disposed when the read ends or the caller
stops early.

Inside the iterator, the steps mirror `SendAsync`: a null check, `DispatchTable.GetStreamInvoker<TItem>`, which casts
the map entry to `StreamInvoker<TItem>` for the same reason the request cast can't fail, and one cancellation check.
`StreamInvoker<TRequest, TItem>.Open` then resolves every component and returns the outermost component's stream, and
the iterator reads it with the same token and yields each item.

**Composition.** Stream behaviors and the handler are wrapped in `StreamBehaviorContinuation` and
`StreamHandlerContinuation`, which derive from `GuardedStreamContinuation`. It reuses the invoked-once guard of
`GuardedContinuation`, and wraps the stream it returns in a `SingleReadStream`, which allows one `GetAsyncEnumerator`
call and forwards it. The guards act only when a continuation is invoked or its stream is opened, never per item.
`ComponentStreams` turns a null stream into an `InvalidOperationException`, as `ComponentTasks` does for tasks.

## Publishing a notification

`Publisher` checks for null, looks up the `NotificationInvoker`, and returns at once when there is none, so a
notification without handlers resolves nothing. Otherwise it checks the token once and calls
`NotificationInvoker<TNotification>.PublishAsync`.

The invoker resolves, for each handler in run order, its behaviors outermost first and then the handler, so every
handler call has its own behavior instances. Then, for each handler, it checks the token and calls the outermost
component. With no behaviors, that is the handler. Otherwise `NotificationBehaviorContinuation` and
`NotificationHandlerContinuation`, both `GuardedContinuation<TNotification, Task>`, chain the behaviors around the
handler, and a `NotificationHandlerCall` carries the notification and the handler type to each behavior. The invoker
awaits each chain on the caller's captured context, except the last, so that the next chain starts there.

## State and caching

| Data | Where | Built | Mutability |
| --- | --- | --- | --- |
| Registrations and the descriptors already emitted | `ArbitratorRegistry`, a singleton instance in the `IServiceCollection` | On each builder call | Changes until frozen, then rejects changes |
| Request and notification invokers | `DispatchTable`, one per root provider | On the first `ISender`, `IPublisher`, or `ArbitratorRegistrations` resolution in that provider | Immutable |
| The registration description | `DispatchMaps`, cached by the registry and shared by every provider built from the collection | When the registry freezes | Immutable |

Nothing else is cached: no handler or behavior instances, no providers or scopes beyond the scoped `Sender` and
`Publisher` holding their own scope's provider, and no requests, responses, tokens, or continuations. The only statics
are the immutable service-key sentinels. Two providers built from one collection each build an identical table from
the shared, frozen registry.

## Tests

- **Black-box.** Tests use only the public API. Each builds its own `ServiceCollection` through `TestHost` and runs
  against the real MS.DI container, so no state is shared between tests.
- **Journal.** Test doubles append events to a per-test `Journal` service, and tests assert on the resulting sequence
  with `Expect`.
- **No timing.** No test relies on sleeps or wall-clock timing to decide its outcome. Coordination uses
  `TaskCompletionSource`.
- **Contract coverage.** Each row of the behavioral contract maps to at least one test. A new row comes with its test.
- **Examples.** `Examples/` compiles and runs the scenarios the README describes.
