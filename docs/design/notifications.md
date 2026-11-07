# Design: Notifications (v0.2.0)

| | |
| --- | --- |
| Status | Implemented |
| Release | 0.2.0 (published as `0.2.0-preview` until .NET 11 is generally available) |
| Date | 2026-11-07 |
| Branch | `feature/arbitrator-v0.2`, from `main` at the v0.1 merge |
| Contract reference | `DESIGN.md` (v0.1). This document extends it and names every section of it that changes |

## Summary

Arbitrator v0.1 sends a request to exactly one handler. Applications also need in-process fan-out: one fact, such as "a customer was deactivated", announced to every part of the application that cares about it. Today they would have to inject every interested component into the code that raises the fact, or write their own dispatcher.

v0.2.0 adds **notifications**. Callers publish a notification through a new scoped `IPublisher`. Arbitrator invokes every handler registered for the notification's exact runtime type, one after another, in registration order, using the caller's dependency injection scope. Publishing is awaited, the first exception stops publication and reaches the caller, and having no handlers is a success.

Notifications are in-process dispatch only. They provide no durability, no transactional publication, no outbox, and no retries.

## Goals

1. Publish a notification to zero or more handlers through a dedicated, scoped publisher.
2. Run handlers sequentially, in one predictable order that follows registration.
3. Keep v0.1's guarantees for notifications:
   - The caller's scope is used, and Arbitrator creates none.
   - Exceptions propagate unwrapped.
   - Failures are reported through the returned task.
   - Registration is explicit and validated early, with actionable diagnostics.
4. Stop publication promptly when the caller's token is canceled between handlers.
5. Change no existing public API and no existing request behavior. v0.2.0 is additive.

## Non-goals

- **Parallel or fire-and-forget publishing.** Exactly one publishing strategy exists, and it is sequential and awaited.
- **Polymorphic matching.** Handlers registered for a base class or interface of the notification don't run.
- **Open-generic notification handlers.** Only closed handler types can be registered.
- **A notification pipeline.** There is no behavior abstraction for notifications, and request behaviors never wrap notification handlers.
- **Durable delivery.** No outbox, persistence, retries, or transactional coupling with the caller's unit of work.
- **Handler priorities or explicit ordering attributes.** Registration order is the only ordering rule.
- **Collecting exceptions from several handlers into an aggregate.**
- **Pre-1.0 renames.** The behavior parameter name `next` and the type name `Unit` are left as they are. They remain in `DESIGN.md` §12.
- **A registry inspection API.**

## Current state

- **v0.1 (merged to `main`).** The library contains:
  - Request contracts (`IRequest`, `IRequest<TResponse>`, the two handler interfaces) and the pipeline behavior contract.
  - The scoped `ISender`.
  - The `AddArbitrator` entry point with `ArbitratorBuilder`, and the single custom exception, `ArbitratorConfigurationException`.
- **Registration** (`src/Arbitrator/Registration/`).
  - **Registry.** `ArbitratorRegistry` collects registrations under a lock and validates each builder call atomically. It rejects calls on read-only collections and freezes when the dispatch table is first built. A collection copied from another gets its own registry.
  - **Collections.** The registry delegates to `RegisteredPipelines` (request types and their planned behaviors) and `RegisteredBehaviors` (behaviors in registration order).
  - **Descriptors.** Handlers and behaviors are registered as transient, keyed descriptors of their own implementation types under a private service key.
- **Dispatch** (`src/Arbitrator/Dispatch/`).
  - **Table.** `DispatchTable` is a singleton built from the frozen registry the first time a provider resolves `ISender`. When it is built, it checks that the provider can resolve every component it refers to.
  - **Sender and invokers.** `Sender` looks up a `RequestInvoker` by the request's exact runtime type.
- **Tests** (`tests/Arbitrator.Tests/`, TUnit). 95 tests run against the real Microsoft.Extensions.DependencyInjection (MS.DI) container through the public API, using a per-test `Journal` and `TestHost`.
- **Size limits.** `ArbitratorRegistry` (225 lines) and `RegistrationErrors` (220 lines) are close to the project's 250-line type limit, which shapes the structure below.

## Proposed solution

Add three public contracts, one builder method, and one scoped service. Implement notification dispatch alongside request dispatch, reusing the existing registry, freeze, keyed-registration, and provider-check machinery rather than building a parallel system.

1. **Contracts.**
   - `INotification` is a marker interface for notification types.
   - `INotificationHandler<TNotification>` handles one notification type.
   - `IPublisher` publishes a notification.
2. **Registration.** `ArbitratorBuilder.AddNotificationHandler<THandler>()` registers every notification contract a handler class implements, at the next notification-handler position.
3. **Dispatch.**
   - The frozen registration state produces a second map: from notification type to a `NotificationInvoker`.
   - The scoped `Publisher` looks up the invoker by the notification's exact runtime type. When there is none, publishing completes immediately.
   - Otherwise the invoker resolves every handler from the caller's scope, then invokes them in order.

**Why this fits.**
- **One mechanism for both kinds of registration.** Notification handlers use the same private-key registration as request handlers, so container validation, the "registered after the provider was built" check, copied-collection isolation, and read-only rejection all apply with no new mechanism.
- **Separate registration methods and contracts.** These keep the strict "exactly one handler" rule for requests separate from the "zero or more" rule for notifications.

## Codebase impact

**No new package.** Notifications are part of the core contract of an in-process mediator and need no extra dependencies. The `Arbitrator` package stays single and dependency-free, which `DESIGN.md` §9.1 already justifies.

**New public API.** All of it is additive, in the `Arbitrator` namespace:

| Member | Kind | Contract |
| --- | --- | --- |
| `INotification` | Interface with no members | Marks a type that can be published. Notifications are dispatched by their exact runtime type |
| `INotificationHandler<TNotification>` | Interface, constrained to `INotification` and invariant | One method, `HandleAsync`. It takes the notification and a cancellation token (no default value) and returns a `Task` that completes when the handler has finished |
| `IPublisher` | Interface | One method, `PublishAsync`. It takes an `INotification` and a cancellation token that defaults to none, and returns a `Task`. It never throws synchronously; every failure is reported through the task |
| `ArbitratorBuilder.AddNotificationHandler<THandler>()` | Builder method | `THandler` is constrained to reference types and annotated to preserve public constructors and interfaces. The method returns the builder, carries `RequiresDynamicCode`, and carries `RequiresUnreferencedCode` only if the trim analyzer requires it |

**API decisions.**
- **`PublishAsync` takes an `INotification`, not a generic parameter.** Handlers are selected by runtime type in any case. This mirrors `ISender.SendAsync(IRequest)`, so call sites never need explicit type arguments.
- **`INotificationHandler<TNotification>` is invariant,** matching the request contracts and the exact-type rule.
- **A type may implement both `IRequest<…>` and `INotification`.** Sending and publishing remain independent operations on independent registrations.

**Changed existing code.**
- **`AddArbitrator`.** It also registers `IPublisher` as a scoped service.
- **`ArbitratorRegistry`.**
  - Gains notification-handler registration under the same lock, freeze check, and atomicity rules.
  - Its freeze result now includes notification invokers.
  - Copying a registry for another collection includes notification handlers.
- **`DispatchTable`.** It holds the notification-invoker map alongside the request map, and its provider check covers notification handler types.
- **`ArbitratorBuilder`.** It gains `AddNotificationHandler` with the same read-only check as the other builder methods.
- **`DESIGN.md`.** The contract document is updated (see Target structure).
- **`README.md`.** Gains a notifications section and contract-table rows.
- **`Arbitrator.csproj`.** The version becomes `0.2.0` with the `preview` suffix.

**Not changed.** The request contracts, `ISender`, pipeline behaviors, request dispatch, and existing messages are untouched. The one exception is the frozen-registry message, which is generalized (see Detailed behavior, item 7).

**No data models, migrations, configuration values, or infrastructure** are involved.

## Target structure

Paths are relative to the repository root. "New" files are added; "changed" files already exist.

**`src/Arbitrator/` (public contracts and entry points)**
- `INotification.cs`: new.
- `INotificationHandler{TNotification}.cs`: new.
- `IPublisher.cs`: new.
- `ArbitratorBuilder.cs`: changed, adds `AddNotificationHandler`.
- `ArbitratorServiceCollectionExtensions.cs`: changed, also registers the scoped publisher.

**`src/Arbitrator/Registration/`**
- **`NotificationHandlerContract.cs`** (new): one notification type paired with one handler type.
- **`NotificationHandlerTypeInspector.cs`** (new): validates a notification handler type and discovers its notification contracts. It rejects:
  - abstract handler types and interfaces
  - types with no notification contract
  - handlers whose notification type is abstract or an interface
- **`RegisteredNotificationHandlers.cs`** (new):
  - Holds the notification handler types in global registration order, and, for each notification type, the ordered handler types and an invoker template.
  - Detects duplicate handler types.
  - Produces the notification-invoker map when frozen and copies itself for another collection.
- **`NotificationRegistrationErrors.cs`** (new): the exceptions for invalid notification registrations. It is kept separate because `RegistrationErrors` is near the type-size limit.
- **`Registrations.cs`** (new): bundles the three registration collections (request pipelines, behaviors, notification handlers).
  - It owns copying all three for another collection, validating and producing the frozen dispatch maps (`DispatchMaps`), planning a new request pipeline from the registered behaviors, and appending a behavior to the pipelines it applies to.
  - `ArbitratorRegistry` keeps only the lock, the frozen flag, ownership, and the orchestration of each registration call, which keeps it within the type-size limit.
- **`ArbitratorRegistry.cs`** (changed):
  - Registration methods delegate to `Registrations`.
  - A new notification-handler registration method.
  - Freezing returns `DispatchMaps` instead of the request map alone.

**`src/Arbitrator/Dispatch/`**
- **`DispatchMaps.cs`** (new): an immutable record holding the frozen request-invoker map and the frozen notification-invoker map.
- **`NotificationInvoker.cs`** (new): the abstract invoker for one notification type. It exposes its component types for the provider check, and publishing given the notification, the scope's provider, and the token.
- **`NotificationInvoker{TNotification}.cs`** (new): the sealed, statically typed implementation, created once per notification type from a template, like request invokers.
- **`Publisher.cs`** (new): the scoped `IPublisher` implementation, bound to the scope that resolved it.
- **`DispatchTable.cs`** (changed):
  - Built from `DispatchMaps`.
  - Adds a lookup by notification type that returns "no invoker" instead of throwing.
  - The provider check covers request and notification component types.

**`src/Arbitrator/Diagnostics/`**
- No change. No notification message needs a formatted contract, so `ContractNames` stays as it is.

**`tests/Arbitrator.Tests/`**
- **`Notifications/`** (new folder):
  - `PublishingTests.cs`: dispatch, order, zero handlers, multiple contracts, exact type, interface-typed reference, struct notifications.
  - `NotificationCancellationTests.cs`
  - `NotificationExceptionTests.cs`
  - `NotificationRegistrationTests.cs`
- **Existing suites** (changed), extended with notification cases:
  - `Scopes/`: scope identity and nested publish.
  - `Disposal/`: handler disposal.
  - `Concurrency/`: concurrent publishes.
  - `ContainerValidation/`: unresolvable notification-handler dependencies, and a singleton capturing `IPublisher`.
  - `Registration/RegistrationLifecycleTests.cs`: late registration, freeze through `IPublisher`, copied collections, read-only collections.
- **`Examples/`** (changed):
  - A `CustomerDeactivated` notification.
  - Two handlers (an audit recorder and a notification-email sender), both writing to the journal.
  - `DeactivateCustomerHandler` publishes the notification after saving.
  - The example test asserts the order of events.
- **`Support/`** (changed): shared notification test doubles where more than one suite uses them.

**Documentation**
- **`docs/design/notifications.md`:** this document.
- **`DESIGN.md`** (changed): sections 1, 3, 4, 5, 6, 7, 8, 10 and 13 describe notifications as part of the contract, and the roadmap marks them delivered.
- **`README.md`** (changed): adds a notifications section.

## Detailed behavior

### 1. Registration

- **What `AddNotificationHandler<THandler>()` registers.** Every closed notification-handler contract `THandler` implements, as one registration at the next **notification-handler position**. Positions count notification-handler registrations across all `AddArbitrator` calls on a collection, independently of behavior positions.
- **Validation order.** The call is all-or-nothing: it validates fully, then changes the registry, then adds descriptors. It is rejected with `ArbitratorConfigurationException`, and nothing changes, when:
  1. The service collection is read-only.
  2. The registry is frozen.
  3. The handler type is already registered as a notification handler.
  4. The handler type is abstract or an interface.
  5. The handler type implements no notification-handler contract.
  6. Any notification type it handles is abstract or an interface. Exact-type dispatch could never select such a handler.
- **No uniqueness rule across handler types.** Any number of different handler types may handle the same notification type. That is the point of notifications.
- **Descriptors.** The handler type is registered once, as a transient keyed descriptor of itself under Arbitrator's private **notification handler key**, which is separate from the key request handlers and behaviors use.
  - **A class used as both a request handler and a notification handler** (via `AddHandler` and `AddNotificationHandler`) is permitted. Because the roles use separate keys, it gets one distinct registration per role. A notification registration added after a provider was built is therefore always detected, even when the class's request registration predates the provider.
- **Each registration call covers one role only.** `AddHandler` never registers notification contracts, and `AddNotificationHandler` never registers request contracts.

### 2. Order

For one notification type, handlers run in ascending notification-handler position, that is, in the order of their `AddNotificationHandler` calls across all `AddArbitrator` calls.

A class implementing contracts for several notification types holds the same position in each of those notification types' lists.

### 3. Publishing (`PublishAsync`)

All failures are reported through the returned task.

1. **Null check.** A null notification faults the task with `ArgumentNullException` (parameter name `notification`). Nothing is resolved.
2. **Handler lookup.** Look up the notification's exact runtime type. If no handler is registered for it, the task completes successfully immediately. The token isn't inspected in that case, because there is nothing to cancel.
3. **Cancellation check before resolution.** If the token is already canceled, the task is canceled with an `OperationCanceledException` carrying the caller's token. No handler is constructed.
4. **Resolution.** Resolve every handler, in order, from the publisher's scope under the private key. A DI failure propagates unwrapped, and no handler has run.
5. **Invocation.** For each handler in order:
   1. If the token is canceled, the task is canceled with an `OperationCanceledException` carrying the caller's token. Remaining handlers don't run, and handlers that already ran keep their effects.
   2. Invoke the handler with the notification and the caller's token, unchanged.
   3. If the handler returns a null task, the task faults with `InvalidOperationException`, using the existing "returned a null Task while handling" message that names the handler and the notification type.
   4. Await the handler's task. An exception from the handler, including an `OperationCanceledException` it throws itself, propagates unchanged as the same instance. Remaining handlers don't run.
6. **Completion.** The task completes successfully after the last handler completes.

**Synchronization context.** Within the handler loop, Arbitrator resumes on the caller's captured context after every handler except the last. That way every handler, not only the first, starts on the same context the caller published from. After the last handler nothing remains to start, so Arbitrator doesn't return to the caller's context; a single-handler publish therefore can't deadlock a caller that blocks on it. This is a deliberate exception to the library's internal use of `ConfigureAwait(false)`, and it applies only to the continuation that starts the next handler.

**Blocking callers.** Publishing must be awaited. With two or more handlers, a caller that blocks on the returned task (`.Wait()`, `.Result`) from a single-threaded synchronization context deadlocks, because the next handler must start on that context while it is blocked. That is the cost of the context guarantee above.

### 4. Scope, lifetimes, and disposal

| Service | Lifetime | Notes |
| --- | --- | --- |
| `IPublisher` (internal `Publisher`) | Scoped | Holds the provider of the scope that resolved it. Arbitrator never creates a scope |
| Notification handlers | Transient | Keyed under the notification handler key, registered as their implementation type. Each publish resolves new instances |

- **Disposal.** Handlers are disposed by the container when their scope ends, never by Arbitrator.
- **Root provider.** Resolving `IPublisher` from the root provider, or capturing it in a singleton, has exactly the consequences `DESIGN.md` §6.2 describes for `ISender`. With scope validation enabled, MS.DI rejects both.
- **Nested publishing and sending.** Handlers and request handlers may inject `IPublisher` or `ISender` and publish or send from inside a handler. Nested operations run in the same scope.
- **No recursion detection.** A handler that publishes a notification which reaches itself recurses until the stack or memory is exhausted.

### 5. Exact-type dispatch

- **Interface-typed references.** A notification held through an `INotification` reference is dispatched by its runtime type.
- **Derived types.** A notification whose runtime type derives from a notification type with handlers reaches **no** handlers, and publishing succeeds. This is a direct consequence of exact-type dispatch together with "zero handlers is success". It is documented prominently, and the README recommends sealed notification types.
- **Struct notifications.** Supported. They are boxed when passed to `PublishAsync`, and each handler receives a copy.

### 6. Concurrency

- **Immutable state.** The notification-invoker map is immutable after it is built.
- **Per-call state.** Resolved handlers and loop position live in the call.
- **Separate instances.** Concurrent publishes in one scope resolve separate handler instances, since handlers are transient.
- **Shared scoped services aren't protected.** Concurrent publishes share the scope's services, as with requests.

### 7. Freeze and provider checks

- **What triggers the freeze.** The dispatch table, and therefore the registry freeze, is built the first time a provider resolves either `ISender` or `IPublisher`.
- **Message change.** The frozen-registry message changes from "once a service provider has attempted to resolve ISender" to "once a service provider has attempted to resolve ISender or IPublisher". Message text isn't part of the compatibility contract.
- **Late registrations.** A notification handler registered after a provider was built is reported when that provider first builds its dispatch table, with the existing message naming the components the provider can't resolve.
- **Copied collections.** Copying a collection's registry for another collection includes notification handlers, so the copy's registrations are independent.

### 8. Diagnostics

All messages name types in C# form and never include notification contents.

| Failure | Stage | Exception | Representative message |
| --- | --- | --- | --- |
| Handler type registered twice | Registration | `ArbitratorConfigurationException` | Notification handler type 'Contoso.Audit.AuditRecorder' is already registered. Each notification handler type may be registered once across all AddArbitrator calls. |
| Abstract handler or interface | Registration | `ArbitratorConfigurationException` | The existing "is abstract or an interface; register a concrete class" message |
| No notification contract | Registration | `ArbitratorConfigurationException` | Type 'Contoso.Audit.AuditService' cannot be registered as a notification handler: it does not implement INotificationHandler<TNotification>. |
| Abstract notification type | Registration | `ArbitratorConfigurationException` | Notification handler 'Contoso.Audit.AuditRecorder' handles 'Contoso.Events.DomainEvent', which is abstract or an interface. Notifications are published by their exact runtime type, so this handler could never run; register a handler for each concrete notification type. |
| Read-only collection, frozen registry, registered after the provider was built | As today | `ArbitratorConfigurationException` | Existing messages; the frozen message names both ISender and IPublisher |
| Null notification | Publish | `ArgumentNullException` | Standard message, parameter `notification` |
| Null task from a handler | Publish | `InvalidOperationException` | Handler 'Contoso.Audit.AuditRecorder' returned a null Task while handling 'Contoso.Customers.CustomerDeactivated'. |
| Canceled token | Publish | `OperationCanceledException` | Standard message; carries the caller's token |
| Handler exception | Publish | Unchanged | None added |

### 9. Behavioral contract additions

These rows extend `DESIGN.md` §7.

| Scenario | Contract |
| --- | --- |
| No handlers for the runtime type | Success. Nothing is resolved, and the token isn't inspected |
| Several handlers | They run sequentially, in registration order, each awaited before the next starts |
| A handler throws | The same exception instance reaches the caller. Later handlers don't run, and earlier handlers keep their effects |
| Token already canceled, with handlers registered | Canceled before any handler is constructed |
| Token canceled while a handler runs | That handler observes it as it chooses. If it completes, the next handler doesn't start and the publish is canceled |
| A handler for the base type of the published runtime type | Not invoked |
| A request behavior is registered | It never wraps notification handlers |
| Publishing from inside a handler | Permitted, in the same scope, with no recursion detection |

## Dependencies

There are no dependency changes. The library still has no package dependencies, and the test project keeps its two packages.

| Package | Used by | Version | Change | License | Advisories |
| --- | --- | --- | --- | --- | --- |
| Microsoft.Extensions.DependencyInjection | Tests only | 11.0.0-rc.3.26522.148 (moves to 11.0.0 at .NET 11 general availability) | None | MIT | None |
| TUnit | Tests only | 1.81.10 (latest stable) | None | MIT | None, including `TUnit.Core`, `TUnit.Engine`, `TUnit.Assertions`, and `Microsoft.Testing.Platform` |

**Audit method and date.** On 2026-11-07, the NuGet V3 flat-container index was used to confirm the latest versions. NuGet's vulnerability feed (`https://api.nuget.org/v3/vulnerabilities/index.json`) was queried for every listed package and transitive package. As a control, the same query returned the known advisories for `System.Text.Json` 8.0.0. NuGetAudit (mode `all`, warnings as errors) runs again on every restore.

## Security considerations

- **No new trust boundary.** Notifications are raised by application code. Arbitrator never builds a notification from external input and never resolves a type from a string, so untrusted input can't select a handler type.
- **Sensitive data.** Notifications often carry personal data, such as customer details. Arbitrator never logs, serializes, stores, or formats notification instances, and its messages contain type names only. The README's guidance not to return Arbitrator's exception messages to clients also covers notifications.
- **Partial effects are by design, not atomic.** When a handler fails, earlier handlers' effects remain. Applications that need all-or-nothing behavior across handlers must use their own transaction around the publish. The README states this explicitly, so nobody assumes otherwise.
- **Resource exhaustion.**
  - **Recursion.** Unbounded recursive publishing is the application's responsibility; Arbitrator doesn't detect cycles.
  - **Fan-out.** The handler count per notification is fixed at registration, so a caller can't amplify it at run time.
  - **No background work.** Arbitrator starts no background work, timers, or threads for publishing.
- **Isolation.** Notification handlers are registered under the private key, so Arbitrator never resolves an application's own `INotificationHandler<>` registrations and never overrides them.

## Testing strategy

All tests use the public API and the real MS.DI container with `ValidateOnBuild` and `ValidateScopes` enabled.

- **Observation.** Each test owns its provider and journal. Coordination uses `TaskCompletionSource` signals, never sleeps or wall-clock timing.
- **Behavioral tests.** Every row in Detailed behavior §9 and every failure in §8 maps to at least one test.
- **Integration.** The examples suite demonstrates a handler that saves and then publishes a domain notification to two handlers.
- **Regression.** The existing 95 tests must keep passing unchanged, except for the frozen-registry message expectation, which matches a substring both the old and new text contain.
- **Repeat runs.** The suite runs five times in a row in Release with no failures.
- **Concurrency.** Concurrent publishes across scopes and within one scope, with assertions on distinct handler instances and correct ordering per publish.
- **Performance.** No targets are set. The optional benchmark milestone from v0.1 may add publishing to the 0, 1 and 3-handler cases later.

## Rollout and rollback

- **Versioning.** `0.2.0-preview` until the .NET 11 general-availability packages are adopted. After that it is `0.2.0`, following the same release gate as v0.1. Under SemVer pre-1.0 rules, the change is additive and non-breaking.
- **Migration.** None. Existing applications get the scoped `IPublisher` registration from their existing `AddArbitrator` call, and nothing else changes for them.
- **Feature flag.** None. Notifications are inert until an application registers notification handlers and publishes.
- **Rollback.** Unlist the package version on NuGet and publish a corrected patch. Consumers pin `0.1.x` to roll back, which requires removing their notification code. No persisted state exists.

## Risks and mitigations

| Risk | Why it could occur | Impact | Detection | Mitigation |
| --- | --- | --- | --- | --- |
| A derived notification silently reaches no handlers | Exact-type dispatch, and zero handlers counts as success | Expected side effects don't happen, with no error | Application tests on the effect | README recommends sealed notification types. `DESIGN.md` §7 states the rule. A future diagnostic could be considered only with evidence |
| Partial effects after a handler fails | Sequential, non-transactional fan-out | An inconsistent state if handlers have side effects | Application tests | Documented in README and `DESIGN.md`. Applications wrap publishing in their own transaction or outbox |
| Handler order becomes an implicit dependency | Order follows registration calls spread across modules | Reordering registrations changes behavior | Tests asserting order | Order is a documented, tested contract. Handlers that depend on each other's effects are discouraged in the README |
| Later handlers start on a different context | A loop using `ConfigureAwait(false)` would move later handlers off the caller's context | Context-dependent handlers misbehave in apps with a synchronization context | A test with a custom synchronization context asserting every handler starts on it | The loop resumes on the captured context between handlers (§3) |
| A caller that blocks on a publish deadlocks | Returning to a blocked single-threaded context | The publish never completes | A test that publishes from a blocking single-threaded context | Arbitrator doesn't return to the context after the last handler, so single-handler publishes complete. With several handlers the guarantee requires the context; the README states publishing must be awaited |
| Registry or error types exceed the 250-line type limit | New registration code is added to near-limit types | A breach of the project's maintainability rules | A size check during implementation | `Registrations` and `NotificationRegistrationErrors` are introduced (Target structure) |
| A late notification registration of a class already registered as a request handler goes unnoticed | Both roles under one key would share a registration | The late-registration check passes on the request registration | A test registering the request role before the build and the notification role after | Separate service keys per role (§1) |
| Long-running handlers delay the caller | Publishing is awaited sequentially | Higher latency on the calling path | Application profiling | By design. Applications that need asynchronous delivery use their own queue; fire-and-forget is a non-goal |

## Pros and cons

**Pros**
- **Small, consistent surface.** Three contracts and one builder method, with the same registration, validation, scope, and exception guarantees as requests.
- **Predictable.** One order rule and one failure rule, with no parallelism and no aggregate exceptions.
- **Reuses proven machinery.** Container validation, the late-registration check, copied-collection isolation, and the read-only check all apply automatically.
- **Additive.** No existing consumer changes.

**Cons**
- **No handlers for base types or interfaces,** so a cross-cutting "every notification" handler must be registered for each type.
- **No notification pipeline.** Logging or timing around handlers must live inside the handlers.
- **Sequential awaiting adds latency** proportional to the number of handlers.

**Alternatives considered**
- **Combined `IMediator`, or `PublishAsync` on `ISender`.** Rejected: consumers would get capabilities they don't need, and the `ISender` name would stop fitting.
- **Open-generic notification handlers.** Rejected for v0.2. They add constraint-based applicability to a second registration path without a demonstrated need.
- **Polymorphic matching.** Rejected: it breaks the exact-type principle and makes order and duplicate rules harder to explain.
- **Reusing `AddHandler` for both roles.** Rejected: one method would follow two different uniqueness rules.
- **Checking cancellation only once, at entry.** Rejected: later handlers would start after cancellation, which is surprising in a loop Arbitrator controls.
- **A notification behavior abstraction.** Deferred until a concrete need appears.
- **Parallel publishing with aggregate exceptions.** Rejected: it adds a second strategy, nondeterministic interleaving, and aggregate error handling, all contrary to the predictability goal.

## Acceptance criteria

**Publishing**
- **PN1.** Publishing a notification with two registered handlers invokes both, in registration order. Each receives the caller's notification instance and token, and the task completes after the second finishes.
- **PN2.** Handler order follows registration order across two `AddArbitrator` calls.
- **PN3.** Publishing a notification type with no handlers completes successfully and constructs nothing, even with an already-canceled token.
- **PN4.** A class implementing handler contracts for two notification types serves both, with a new instance per publish.
- **PN5.** A notification held through an `INotification` reference reaches the handlers of its runtime type.
- **PN6.** A notification whose type derives from a handled type reaches no handlers, and publishing succeeds.
- **PN7.** A struct notification reaches its handler with its payload intact.
- **PN8.** A request behavior registered as an open behavior never runs during publishing.

**Cancellation**
- **PC1.** With handlers registered and a token already canceled, the task is canceled, the exception carries the caller's token, and no handler is constructed.
- **PC2.** When the first handler cancels the caller's token source and completes, the second handler doesn't run, and the task is canceled.
- **PC3.** An `OperationCanceledException` thrown by a handler propagates as the same instance, and later handlers don't run.
- **PC4.** Every handler that runs receives the caller's token unchanged.

**Exceptions and failures**
- **PE1.** A handler's exception reaches the caller as the same instance, with its stack trace. Later handlers don't run, and earlier handlers' journal entries remain.
- **PE2.** A handler returning a null task faults publishing with `InvalidOperationException` naming the handler and the notification type.
- **PE3.** A handler constructor exception propagates unwrapped, and no handler has run.
- **PE4.** Publishing a null notification returns a faulted task with `ArgumentNullException` for parameter `notification`, without throwing synchronously.

**Registration**
- **PR1.** Each registration error in Detailed behavior §8 throws `ArbitratorConfigurationException` from the offending call, and the service collection's descriptor count is unchanged.
- **PR2.** Two different handler types for one notification type register successfully.
- **PR3.** A class registered with both `AddHandler` and `AddNotificationHandler` serves requests and notifications.
- **PR4.** A notification handler registered after a provider was built is reported when that provider first resolves `IPublisher`, naming the handler.
- **PR5.** Resolving `IPublisher` freezes the registry. A later builder call fails with the message naming both `ISender` and `IPublisher`.
- **PR6.** On a copied collection, a notification handler added to the copy isn't invoked by the original's providers.
- **PR7.** `AddArbitrator` registers exactly one `IPublisher` descriptor, however many times it is called.

**Container validation, scopes, disposal, concurrency**
- **PV1.** With `ValidateOnBuild` enabled, a notification handler with an unresolvable dependency fails `BuildServiceProvider`, naming the handler.
- **PV2.** With scope validation enabled, a singleton depending on `IPublisher` fails container validation.
- **PS1.** A handler's scoped dependency is the instance the publisher's scope resolves. A publish from inside a request handler, and a send from inside a notification handler, share the scope.
- **PS2.** A disposable notification handler is disposed when its scope ends, not when publishing completes.
- **PX1.** 1,000 concurrent publishes across separate scopes each run their handlers in order, with no cross-talk.
- **PX2.** With a custom synchronization context installed by the test, every handler in a publish starts on that context.

**Documentation and quality**
- **PD1.** `DESIGN.md` and `README.md` describe notifications as specified here, including exact-type dispatch, partial effects, and the recommendation to seal notification types.
- **PD2.** The build has zero warnings in Debug and Release, including the trimming and AOT analyzers, with no new suppressions. `dotnet format --verify-no-changes` is clean.
- **PD3.** Every new or changed method stays within the project's method budget, and every type stays within 250 lines.
- **PD4.** All tests pass, including the existing 95, in five consecutive Release runs.
