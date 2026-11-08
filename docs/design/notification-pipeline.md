# Design: Notification pipeline (v0.6.0)

| | |
| --- | --- |
| Status | Implemented |
| Release | 0.6.0 (published as `0.6.0-preview.1` until .NET 11 is generally available) |
| Date | 2026-11-08 |
| Branch | `feature/notification-pipeline`, from `main` at `ab65a0e` |
| Issue | [#7 Notification pipeline](https://github.com/CodeByDylan/Arbitrator/issues/7) |
| Contract reference | [`contract.md`](../contract.md). This document names every section of it that changes |
| Decisions | [35](../decisions/0035-per-handler-notification-behaviors.md), [36](../decisions/0036-notification-behaviors-share-registration.md), [37](../decisions/0037-behavior-instances-per-handler.md). Decision 22 is partly superseded |

## Summary

Arbitrator publishes a notification to every handler registered for its exact type, one after another. Unlike requests, notifications have no pipeline (decision 22). Anything that should happen around every handler, such as logging which handler ran and how long it took, isolating one handler's failure so the others still run, or a transaction per handler, has to be repeated inside each handler.

v0.6.0 adds **notification behaviors**. An `INotificationBehavior<TNotification>` wraps each handler call. With three handlers, the behavior chain runs three times, each time around one handler, and each time the behavior receives the notification, the handler's implementation type, a continuation, and the token. Notification behaviors are registered with the existing `AddBehavior` and `AddOpenBehavior`, take positions in the one global behavior order, and follow every rule request behaviors follow: transient instances, the continuation invoked at most once, null tasks reported by name, and exceptions passed through unchanged.

## Goals

1. Let cross-cutting code run around every notification handler, once per handler call, without changing the handlers.
2. Tell each behavior which handler it wraps.
3. Reuse the existing registration methods, behavior order, validation, and diagnostics.
4. Keep every existing notification guarantee: exact-type dispatch, sequential publishing in registration order, the caller's scope, the cancellation check before each handler, and unwrapped exceptions.
5. Describe notification behaviors in `ArbitratorRegistrations`.
6. Change nothing for applications that register no notification behaviors.

## Non-goals

- **A behavior around the whole publish** (decision 35). Callers can wrap `PublishAsync`.
- **Parallel publishing, retries, or a built-in "continue on failure" mode.** A behavior may catch a handler's exception, but Arbitrator adds no policy.
- **Request behaviors wrapping notification handlers,** or notification behaviors wrapping request handlers.
- **Passing the handler instance** to behaviors (decision 35).
- **Discovering behaviors by assembly scanning.** Behaviors stay explicit.
- **Polymorphic or open-generic notification handlers** (issue #6).
- **Native AOT support.**

## Current state

- **Contracts.** `INotification`, `INotificationHandler<TNotification>` with `HandleAsync(notification, cancellationToken)`, and `IPublisher.PublishAsync`.
- **Registration.**
  - `AddNotificationHandler` records each handler type in a `NotificationPlan` per notification type, held by `RegisteredNotificationHandlers`, and adds one keyed descriptor under `ArbitratorServiceKey.NotificationHandlers`. It carries `RequiresDynamicCode` only.
  - `AddBehavior` and `AddOpenBehavior` accept request and stream behaviors (decision 28). `BehaviorTypeInspector` produces a `ClosedBehaviorRegistration`, holding its request contracts, or an `OpenBehaviorRegistration` with a `PipelineKind`. Both derive from the `closed` record `BehaviorRegistration`, and `RegisteredBehaviors` keeps them in global order.
  - Behaviors are placed into `PipelinePlan`s through `BehaviorPlacement`, with the "would run twice" check, and open behavior instantiations get one descriptor each under `ArbitratorServiceKey.RequestComponents`.
- **Freeze.** `Registrations.CreateDispatchMaps` checks that every closed behavior applies to some request pipeline, and builds the invokers and the `ArbitratorRegistrations` description.
- **Publishing.** `Publisher` checks for null, looks up the `NotificationInvoker` (succeeding immediately when there is none), and checks the token. `NotificationInvoker<TNotification>` resolves every handler, then for each one checks the token, calls it, reports a null task by name, and awaits it, resuming on the caller's context only when another handler follows.
- **Inspection.** `NotificationRegistration` has `NotificationType` and `HandlerTypes`.
- **Sizes.** `ArbitratorBuilder` is 243 lines, `RegistrationErrors` 232, and `ArbitratorRegistry` 220, against the 250-line type limit.

## Proposed solution

1. **Contracts.** Add `INotificationBehavior<TNotification>` and the `NotificationContinuation` delegate.
2. **Registration model.**
   - `ClosedBehaviorRegistration` gains the notification types it implements `INotificationBehavior<>` for.
   - A new `OpenNotificationBehaviorRegistration` joins the `closed` `BehaviorRegistration` hierarchy, so every `switch` over registrations must handle it.
   - `BehaviorTypeInspector` recognizes both.
3. **Planning.** `NotificationPlan` gains planned behaviors, with the same "would run twice" check as `PipelinePlan`. Both plans implement a small internal interface for placing behaviors, so `BehaviorPlacement` and `Registrations.AppendBehavior` serve both. Behaviors are placed incrementally, so the order of handler and behavior registrations doesn't matter.
4. **Descriptors.** Closed notification behaviors and every open notification behavior instantiation get keyed transient descriptors under a new `ArbitratorServiceKey.NotificationBehaviors`.
5. **Dispatch.** `NotificationInvoker<TNotification>` holds the closed behavior types alongside the handler types. For each handler call, it builds a guarded chain from new `NotificationBehaviorContinuation` and `NotificationHandlerContinuation` types, which reuse `GuardedContinuation<TNotification, Task>`.
6. **Inspection.** `NotificationRegistration` gains `BehaviorTypes`.

To keep types within 250 lines, behavior registration logic moves from `ArbitratorRegistry` into `Registrations`, as handler registration did for scanning. The registry methods keep their signatures and only take the lock and check the freeze.

**Why this fits.** It extends the existing behavior model instead of adding a parallel one. Decisions 8, 13, 28, and 31 already define order, resolution, registration, and guards. The `closed` registration hierarchy makes the compiler point out every place the new kind must be handled.

## Codebase impact

- **Library (`src/Arbitrator`).**
  - **Public:** two new types and one new property, `NotificationRegistration.BehaviorTypes`. `AddNotificationHandler` gains `RequiresUnreferencedCode` (decision 36).
  - **Internal:**
    - a new service key
    - a new open notification behavior registration
    - notification types on closed behavior registrations
    - behaviors on `NotificationPlan`
    - a plan interface shared by both plan types
    - notification continuations
    - kind-aware error messages
    - behavior registration moved from `ArbitratorRegistry` to `Registrations`
  - **Project file:** the version becomes `0.6.0` with suffix `preview.1`.
- **New library or package:** none. Decision 18 stands.
- **Public API files:** new entries in `PublicAPI.Unshipped.txt`.
- **Tests:** two new test files under `Notifications/`, support behaviors, inspection additions, and an example.
- **Documentation:** `contract.md`, `architecture.md`, `README.md`, this design, and decisions 35–37, with decision 22's status updated.
- **Configuration, infrastructure, CI, persistence, migrations:** none.

## Target structure

Only new and changed paths are listed.

- `src/Arbitrator/`
  - `INotificationBehavior{TNotification}.cs`: new. The notification behavior contract.
  - `NotificationContinuation.cs`: new. The continuation a notification behavior receives.
  - `NotificationRegistration.cs`: changed. Adds `BehaviorTypes`.
  - `ArbitratorBuilder.cs`: changed.
    - Its XML documentation names the notification behavior contract.
    - `AddNotificationHandler` gains `RequiresUnreferencedCode`.
    - If the type would exceed 250 lines, the four scanning members move to a new `ArbitratorBuilder.Scanning.cs` partial file.
  - `Arbitrator.csproj`, `PublicAPI.Unshipped.txt`: changed.
  - `Registration/`
    - `OpenNotificationBehaviorRegistration.cs`: new. An open notification behavior. It closes over a notification type when the runtime accepts its constraints.
    - `IBehaviorPlan.cs`: new. The internal interface for a plan that behaviors are placed into: its planned behaviors and the service key they are registered under, a check that a behavior can be appended, appending it, and whether it contains a registration.
    - `PlannedBehaviors.cs`: new. The list logic both plans share: find a duplicate closed type, append, check for a registration, list the closed types, and copy.
    - `OpenRegistration.cs`: new. A `closed` intermediate record that both open behavior registrations derive from, so a `switch` over an open registration is exhaustive.
    - `ClosedBehaviorRegistration.cs`: changed. Adds the notification types it applies to.
    - `BehaviorTypeInspector.cs`: changed.
      - Closed behaviors: finds request, stream, and notification behavior contracts.
      - Open behaviors: accepts a one-parameter notification definition or a two-parameter request or stream definition, implementing exactly one behavior contract.
    - `RegisteredBehaviors.cs`: changed. Plans behaviors for a notification type as well as for a handler contract.
    - `NotificationPlan.cs`: changed. Holds planned behaviors, implements `IBehaviorPlan`, and describes its behaviors.
    - `PipelinePlan.cs`, `BehaviorPlacement.cs`: changed. Placement refers to `IBehaviorPlan`.
    - `RegisteredNotificationHandlers.cs`: changed. Places notification behaviors into its plans, creates plans with the behaviors registered so far, and checks closed behaviors.
    - `Registrations.cs`: changed.
      - It registers closed and open behaviors for both plan kinds, moved from `ArbitratorRegistry`.
      - It emits open notification behavior descriptors for new notification plans.
      - It checks closed behaviors against both plan kinds at freeze.
    - `ArbitratorRegistry.cs`: changed. The behavior methods delegate to `Registrations`.
    - `ComponentDescriptors.cs`: changed. Adds the notification behavior descriptor.
    - `BehaviorConflict.cs`: changed. Names a target type, request or notification.
    - `RegistrationErrors.cs`, `BehaviorRegistrationErrors.cs`: the behavior factories moved to the new `BehaviorRegistrationErrors.cs`, keeping both types under 250 lines. Messages name notification contracts and say "notification" where they apply.
  - `Dispatch/`
    - `ArbitratorServiceKey.cs`: changed. Adds `NotificationBehaviors`.
    - `NotificationInvoker.cs`, `NotificationInvoker{TNotification}.cs`: changed. Hold behavior types, expose handler and behavior types separately for the provider check, and run each handler through its chain.
    - `NotificationBehaviorContinuation{TNotification}.cs`, `NotificationHandlerContinuation{TNotification}.cs`: new. Guarded continuations for notification chains.
    - `NotificationHandlerCall{TNotification}.cs`: new. The notification and handler type a behavior continuation passes on.
    - `ComponentResolver.cs`, `PipelineInvoker.cs`, and the request and stream invokers: changed. Resolution takes the service key, and `PipelineInvoker` resolves its components through two protected helpers.
    - `DispatchTable.cs`: changed. Checks notification behavior types under their own key.
  - `Diagnostics/ContractNames.cs`: changed. Formats `INotificationBehavior<…>`.
- `tests/Arbitrator.Tests/`
  - `Notifications/NotificationPipelineTests.cs`: new. Wrapping, order, handler type, short-circuit, failure isolation, guards, null tasks, cancellation, context, and instance identity.
  - `Notifications/NotificationBehaviorRegistrationTests.cs`: new. Shapes, constraints, shared positions, "would run twice", closed behavior without handler, container validation, late registration, and registration order independence.
  - `Inspection/NotificationInspectionTests.cs`: changed. Covers `BehaviorTypes`.
  - `Support/`: new `RecordingNotificationBehavior`, `FirstNotificationBehavior`, and `SecondNotificationBehavior`.
  - `Examples/`: a new `HandlerTimingBehavior<TNotification>` that logs each handler's name and duration, registered in `ExampleTests`.
- `docs/`
  - `design/notification-pipeline.md`: new (this document).
  - `decisions/0035`–`0037`: new. `decisions/0022`: status changed.
  - `contract.md`, `architecture.md`: changed, as listed under "Rollout and rollback".
- `README.md`: changed.

## Detailed behavior

### 1. Public API

| Member | Contract |
| --- | --- |
| `INotificationBehavior<TNotification>` where `TNotification : INotification` | One method, `HandleAsync(TNotification notification, Type handlerType, NotificationContinuation next, CancellationToken cancellationToken)`, returning `Task` |
| `NotificationContinuation` | A delegate taking a `CancellationToken` and returning `Task`: the rest of the chain for one handler. It may be invoked at most once |
| `NotificationRegistration.BehaviorTypes` | `IReadOnlyList<Type>`: the closed notification behavior types that run around each handler, outermost first. Empty when none applies |

- **Shape rules.** As for the other contracts:
  - the interface is invariant
  - the constraint is restated by open behaviors
  - the method is named `HandleAsync`, returns `Task`, and takes the token explicitly
  - `next` carries the same `CA1716` justification as request behaviors
- **`handlerType`.** It is the handler's implementation type, as registered. Behaviors use it for logs and metrics, and must not resolve or construct it.

### 2. Registration

- **Closed behaviors.** `AddBehavior<TBehavior>()` accepts a class implementing any number of `IPipelineBehavior<,>`, `IStreamPipelineBehavior<,>`, and `INotificationBehavior<>` contracts.
  - A closed notification behavior applies to every notification type it implements `INotificationBehavior<>` for.
  - A class implementing none of the three is rejected with the reworded "not a behavior" error.
- **Open behaviors.** `AddOpenBehavior(type)` accepts a generic type definition that implements exactly one behavior contract with its type parameters in order:
  - two parameters for `IPipelineBehavior<,>` or `IStreamPipelineBehavior<,>`
  - one parameter for `INotificationBehavior<>`

  Anything else, including a definition implementing a request and a notification behavior contract, is rejected with the reworded "invalid open behavior" error.
- **Applicability.**
  - A closed notification behavior applies to a notification type when it implements `INotificationBehavior<>` for that exact type.
  - An open one applies when closing it over the notification type passes the runtime's constraint check, as in `contract.md` §4.2.
  - Notification behaviors never apply to request or stream pipelines, and the reverse.
- **Order.** Notification behaviors take positions in the one global order shared with request and stream behaviors (decision 36). The first registered is the outermost. Registering a handler before or after a behavior makes no difference.
- **Placement.**
  - Registering a notification behavior places it into every existing notification plan it applies to.
  - Creating a notification plan, when the first handler for a type is registered, places every applicable behavior registered so far, in global order.
  - The "would run twice" check applies: a closed behavior that equals the instantiation of a registered open behavior for a notification type with handlers is an error.
- **Descriptors.** Every descriptor is transient, keyed under `ArbitratorServiceKey.NotificationBehaviors`, and registered as its implementation type:
  - one per closed type implementing `INotificationBehavior<>`, added by `AddBehavior`
  - one per open instantiation that applies to a notification type with handlers, added by whichever call creates that combination

  A closed behavior that also implements request contracts additionally keeps its existing request-components descriptor.
- **Trimming annotations.**
  - `AddNotificationHandler` gains `RequiresUnreferencedCode`, because it closes open notification behaviors registered earlier.
  - `AddBehavior` still needs no annotation.
  - `AddOpenBehavior` and the scanning members already carry both annotations.
- **Freeze.** A closed behavior that applies to no request pipeline and no notification plan fails the first `ISender`, `IPublisher`, or `ArbitratorRegistrations` resolution, with the reworded "closed behavior without handler" error.
- **Scanning.** Unchanged: scans register handlers only. A class that implements only `INotificationBehavior<>` isn't a candidate.

### 3. Publishing

The steps of `contract.md` §4.3 become:

1. A null notification faults the task with `ArgumentNullException`.
2. If no handler is registered for the runtime type, publishing succeeds immediately. No behavior is resolved, and the token isn't inspected.
3. Otherwise the token is checked. Then, for each handler in run order, its applicable behaviors are resolved, outermost first, followed by the handler, all from the publisher's scope and before anything runs. Each handler call gets its own behavior instances (decision 37). DI exceptions propagate unwrapped.
4. For each handler in turn:
   1. The token is checked.
   2. The outermost component of that handler's chain is called with the notification, the handler type (for behaviors), and the token. Each continuation passes on the token it was given.
   3. A null task from a behavior or the handler is reported by name, as an `InvalidOperationException` naming the component and the notification type.
5. The first exception that reaches the outermost component stops publication and reaches the caller unwrapped. Handlers that already ran keep their effects. A behavior that catches its handler's exception and completes normally lets publication continue with the next handler.

- **Short-circuit.** A behavior that never invokes `next` skips its handler, which was already constructed, and publication continues with the next handler.
- **Guard.** Invoking `next` a second time throws `InvalidOperationException` from `next`, with the existing message, naming the behavior and the notification type. The handler runs once.
- **Context.** After every handler's chain except the last, Arbitrator resumes on the caller's captured context. So the outermost component of every handler's chain starts on the context the caller published from. Where inner components start depends on how the behaviors await. Publishing still must be awaited.
- **No behaviors.** A handler with no applicable behaviors is called directly, exactly as today, and no continuations are allocated.

### 4. Inspection

- `NotificationRegistration.BehaviorTypes` lists the closed behavior types that run around each of that notification type's handlers, outermost first. Every handler of a notification type gets the same behavior types, because applicability depends only on the notification type.
- `RequestRegistration.BehaviorTypes` never contains notification behaviors.

### 5. Edge cases

| Case | Outcome |
| --- | --- |
| Notification type with no handlers but a matching open behavior | Nothing is resolved or run, and the behavior appears nowhere in the inspection description |
| Closed notification behavior for a notification type with no handlers | The first resolution fails, unless the class also applies to some other registered pipeline or plan |
| A closed class implementing `IPipelineBehavior<A, R>` and `INotificationBehavior<N>` | One registration and one position. It runs around `A`'s handler and around each of `N`'s handlers |
| A class registered as a notification handler and as a notification behavior | Allowed. The two roles use different service keys |
| A behavior that throws before invoking `next` | The handler isn't invoked. Publication stops, and the exception reaches the caller unchanged |
| A behavior that catches its handler's exception and rethrows another | The new exception stops publication and reaches the caller |
| A derived notification type | Reaches no handlers, so no behaviors run, as today |
| Token canceled while a behavior runs | Forwarded only. The next check is before the next handler's chain |

### 6. Diagnostics

New and changed messages in `contract.md` §7.2. The wording is representative.

| Failure | Stage | Representative message |
| --- | --- | --- |
| Invalid closed behavior (changed) | Registration | `Type 'Contoso.Pipeline.Auditor' cannot be registered as a behavior: it implements none of IPipelineBehavior<TRequest, TResponse>, IStreamPipelineBehavior<TRequest, TItem>, or INotificationBehavior<TNotification>.` |
| Invalid open behavior (changed) | Registration | `Open behavior 'Contoso.Pipeline.AuditBehavior<T>' must be a generic type definition that implements exactly one behavior contract using its type parameters in order: IPipelineBehavior<TRequest, TResponse> or IStreamPipelineBehavior<TRequest, TItem> with two type parameters, or INotificationBehavior<TNotification> with one.` |
| Closed behavior with no handler (changed) | First resolution | `Behavior 'Contoso.Audit.ArchiveAuditBehavior' implements INotificationBehavior<Contoso.Events.CustomerArchived>, but none of the types it applies to has a registered handler.` |
| Behavior would run twice (changed) | Registration | `Behavior 'Contoso.Pipeline.TimingBehavior<Contoso.Events.CustomerArchived>' would run twice for notification 'Contoso.Events.CustomerArchived': it is produced by 'Contoso.Pipeline.TimingBehavior<>' at position 1 and by 'Contoso.Pipeline.TimingBehavior<Contoso.Events.CustomerArchived>' at position 2.` Requests keep "for request" |
| Continuation invoked twice | Publish | The existing message, naming the behavior and the notification type |
| Null `Task` from a notification behavior | Publish | The existing behavior message, naming the behavior and the notification type |

### 7. Behavioral contract additions (`contract.md` §6)

New rows 63 to 72:

- **63 Wrapping.** Applicable notification behaviors run around each handler call separately, in global registration order, outermost first.
- **64 Arguments.** A notification behavior receives the publisher's notification instance, the handler's implementation type, and the token.
- **65 Applicability.** Closed: exact notification type. Open: the runtime constraint check. Request and notification behaviors never wrap each other's handlers.
- **66 Short-circuit.** A behavior that doesn't invoke `next` skips its handler, which was already constructed, and publication continues.
- **67 Failure isolation.** A behavior may catch its handler's exception. Arbitrator imposes nothing, and publication continues if the behavior completes normally.
- **68 Continuation reuse.** Invoking `next` twice throws `InvalidOperationException`, and the handler runs once.
- **69 Null task from a notification behavior.** `InvalidOperationException` naming the behavior and the notification type.
- **70 Instances and resolution.** Each handler call gets its own behavior instances, and every behavior and handler is resolved before the first runs.
- **71 Cancellation.** Checked before each handler's chain, and otherwise only forwarded.
- **72 No handlers.** No notification behavior is resolved or run.

Row 30 (no handlers) and row 32 (a handler throws) still hold for publishes without behaviors. Row 60 (inspection of notifications) gains `BehaviorTypes`.

## Dependencies

No dependency is added, updated, replaced or removed.

| Dependency | Change | Audit result |
| --- | --- | --- |
| None | n/a | `dotnet list package --vulnerable --include-transitive` against nuget.org on 2026-11-08 reported no vulnerable packages in `Arbitrator` or `Arbitrator.Tests` |

## Security considerations

- **No new trust boundary.** Behaviors are application code registered explicitly, and they run in the caller's scope like handlers.
- **Handler identity, not instances.** Behaviors receive a `Type`, not the handler. They can't call the handler outside `next`, so the guard can't be bypassed.
- **Failure isolation is the application's choice.** A behavior that swallows exceptions can hide failures. The README example logs before continuing, and the contract states that Arbitrator imposes nothing.
- **No data in messages.** Diagnostics name types only. Arbitrator never formats notifications.
- **Resource use.** Resolution grows to N × M instances per publish (decision 37), bounded by the application's own registrations. There are no buffers, threads, timers, or caches.

## Testing strategy

All tests are TUnit tests through the public API against MS.DI, using `TestHost`, `Journal`, and `Expect`, with ordered assertions using `CollectionOrdering.Matching`. None uses timing.

- **Pipeline.**
  - Order with two handlers and two behaviors: each handler is wrapped separately, outermost first.
  - The `handlerType` argument for each handler.
  - Open, closed, and constrained behaviors.
  - Behaviors registered across two `AddArbitrator` calls, before and after handlers.
  - Short-circuit, failure isolation, a behavior throwing before `next`, and a translated exception.
  - The `next` guard, and null tasks from an outer and an inner behavior.
  - Cancellation before the second handler's chain.
  - Context: each handler's outermost behavior starts on a marked synchronization context.
  - Distinct behavior instances per handler call.
  - No resolution without handlers.
- **Registration.**
  - Closed behaviors implementing notification contracts only, and alongside request contracts.
  - Open one-parameter definitions.
  - Shape errors: wrong arity, both kinds.
  - Shared position numbering with request behaviors.
  - "Would run twice" for a notification.
  - A closed behavior for a notification without handlers, at first resolution.
  - Container validation of a notification behavior's dependencies.
  - Registrations after the provider was built, a frozen registry, copied collections, and a class that is both a notification handler and a notification behavior.
- **Inspection.** `BehaviorTypes` on notification entries matches the behaviors that run, and request entries never contain notification behaviors.
- **Example.** The README's timing behavior runs in `ExampleTests` around both deactivation handlers.
- **Regression.** The existing 236 tests pass. Assertions on reworded messages check type names, as `contract.md` §7.2 prescribes.
- **Analyzers.** The Release build has zero warnings, including trimming and AOT analyzers, with no new suppressions.

## Rollout and rollback

- **Release.** The work merges to `main` through a pull request that must pass "Build and test". It ships as `0.6.0-preview.1`. The release notes say:
  - the feature is additive
  - `AddNotificationHandler` now carries `RequiresUnreferencedCode`
  - four registration messages are reworded
- **Documentation updates in the same change.**
  - `contract.md`:
    - §1 principle 1 becomes "one pipeline abstraction for each kind of message".
    - §3.1 lists the new contracts.
    - §3.2: `AddBehavior` and `AddOpenBehavior` accept notification behaviors.
    - §4.2 covers notification behavior applicability.
    - §4.3: the "No pipeline" bullet is replaced, and the steps and the "Context" bullet are updated.
    - §4.7 adds the new annotation on `AddNotificationHandler`.
    - §5.1 adds notification behaviors as transient, keyed under their own key.
    - §5.4: duplicates and "would run twice" apply to notifications.
    - §5.5 gains the new registration errors.
    - §5.6 adds `BehaviorTypes`.
    - §6 gains rows 63–72, and row 60 is updated.
    - §7.1 and §7.2 gain the messages above.
  - `architecture.md`:
    - registration covers notification behaviors and the third key
    - publishing covers the per-handler chain
    - the source layout is updated
  - `README.md`: the "Notifications" section gains notification behaviors, with the timing example and a failure-isolation note. "The contract in brief" gains a row.
  - `docs/design/notification-pipeline.md`: the status becomes "Implemented". Issue #7 is closed after the merge.
- **Migration:** none.
- **Feature flag:** none. Behavior only appears when notification behaviors are registered.
- **Rollback.** Consumers pin `0.5.0-preview.1`. A defective preview is unlisted and replaced by a higher version.

## Risks and mitigations

| Risk | Cause | Impact | Detection | Mitigation |
| --- | --- | --- | --- | --- |
| Behaviors silently swallow failures | Failure isolation is easy to write | Lost errors | Code review | The README example logs before continuing, and the contract states it's the behavior's responsibility |
| Allocation grows with N × M | Per-handler instances | Slower publishes with many handlers and behaviors | Not measured | Accepted (decision 37). No behaviors means no cost |
| A missed `switch` arm or plan kind | Adding a third behavior kind | Behaviors misapplied or ignored | Compiler errors on the `closed` hierarchy, and tests | The `closed` hierarchy forces exhaustive switches, and tests cover each kind |
| Trimmed applications see a new warning | `RequiresUnreferencedCode` on `AddNotificationHandler` | A build break where warnings are errors | Analyzer output | Release notes. Arbitrator doesn't support Native AOT anyway |
| Type size limits | Builder 243, `RegistrationErrors` 232, registry 220 lines | A maintainability standard is breached | Measuring during implementation | Behavior registration moves into `Registrations`, with the fallback splits named under Target structure |
| Tests asserting old message wording | Four reworded messages | Test failures in this repository | The test run | Assertions check type names, and any exact-text assertion is updated to the new wording |

## Pros and cons

**Pros**

- Per-handler cross-cutting code, including failure isolation, without touching handlers.
- One behavior model and one order for requests, streams, and notifications.
- No change for applications that register no notification behaviors.

**Cons**

- A behavior that should run once per publish needs another approach.
- More registration complexity: a third behavior kind, a third service key, and plan placement for notifications.
- A new trimming annotation on `AddNotificationHandler`.

**Alternatives considered**

- **A whole-publish chain, or both kinds.** Rejected in decision 35.
- **Passing the handler instance, or nothing.** Rejected in decision 35.
- **Separate registration methods or order.** Rejected in decision 36.
- **Shared behavior instances per publish.** Rejected in decision 37.
- **Making request behaviors apply to notifications.** Rejected: a request behavior's `TResponse` and continuation don't fit notifications.

## Acceptance criteria

Each criterion has at least one test, in `tests/Arbitrator.Tests/Notifications/` unless another place is named.

- **NP1** With two handlers and two notification behaviors, each handler runs inside its own chain, in global order, outermost first, and the handlers run in registration order.
- **NP2** Each behavior receives the publisher's notification instance, the token, and the implementation type of the handler it wraps.
- **NP3** Closed, open, and constrained notification behaviors apply exactly where the contract says. Request behaviors never wrap notification handlers, and notification behaviors never wrap request or stream handlers.
- **NP4** Notification behaviors share positions with request behaviors, and registering handlers before or after behaviors makes no difference.
- **NP5** A short-circuiting behavior skips its handler, and publication continues. A behavior that swallows its handler's exception lets publication continue. A behavior's own exception stops publication and reaches the caller unchanged.
- **NP6** Invoking `next` twice throws `InvalidOperationException`, and the handler runs once. A null task from a notification behavior is reported by name.
- **NP7** Each handler call gets distinct behavior instances, and all components are resolved before the first runs. An activation failure runs nothing.
- **NP8** The token is checked before each handler's chain. A canceled token stops publication before the next handler's outermost behavior.
- **NP9** Each handler's outermost component starts on the caller's synchronization context.
- **NP10** Publishing a notification with no handlers resolves and runs no behavior.
- **NP11** Invalid shapes are rejected:
  - a closed type with no behavior contract
  - an open definition with the wrong number of type parameters
  - an open definition implementing request and notification contracts

  So is a "would run twice" conflict for a notification, with the reworded messages. A closed notification behavior for a type without handlers fails the first resolution.
- **NP12** Container validation reports a notification behavior's missing dependency. Registrations after the provider was built are reported.
- **NP13** `NotificationRegistration.BehaviorTypes` lists the behaviors that run, outermost first, in `tests/Arbitrator.Tests/Inspection/`.
- **NP14** The README's timing behavior runs in `ExampleTests`.
- **NP15** The existing tests pass. The Release build has zero warnings with no new suppressions. The public API files list exactly the new members, and `AddNotificationHandler` carries both trimming annotations.
- **NP16** `contract.md`, `architecture.md`, and `README.md` contain the updates listed under "Rollout and rollback". The version is `0.6.0-preview.1`.
