# Design: Configurable handler and behavior lifetimes (v0.7.0)

| | |
| --- | --- |
| Status | Implemented |
| Release | 0.7.0 (published as `0.7.0-preview.1` until the stable SDK is pinned) |
| Date | 2026-11-12 |
| Branch | `feature/configurable-lifetimes`, from `main` at `9336ae6` |
| Issue | [#8 Configurable handler and behavior lifetimes](https://github.com/CodeByDylan/Arbitrator/issues/8) |
| Contract reference | [`contract.md`](../contract.md). This document names every section of it that changes |
| Decisions | [38](../decisions/0038-configurable-component-lifetimes.md), [39](../decisions/0039-lifetime-per-scan.md), [40](../decisions/0040-inspection-reports-lifetimes.md). Decision 10 is partly superseded |

## Summary

Every handler and behavior Arbitrator registers is transient (decision 10): each send, read, or publish constructs new instances. That's the safest default, but some applications construct expensive components, such as compiled rules or large lookup tables, on every call. Others need state that lives for one scope, such as a per-request cache shared by several sends.

v0.7.0 makes **component lifetimes configurable**. Each registration method gains an overload taking MS.DI's own `ServiceLifetime`:
- `AddHandler`, `AddNotificationHandler`, `AddBehavior`, and `AddOpenBehavior`
- the four assembly scanning methods

Transient, scoped, and singleton are all allowed. The existing overloads keep registering transient components, so nothing changes for current applications. `ArbitratorRegistrations` reports each component's lifetime.

Arbitrator adds no lifetime rules of its own. MS.DI applies the chosen lifetime, and container validation reports the one dangerous combination: a singleton that depends on a scoped service.

## Goals

1. Let an application register any handler or behavior as transient, scoped, or singleton.
2. Let a scan register every handler it finds with one chosen lifetime.
3. Keep transient as the default, so existing registrations behave exactly as today.
4. Keep every other rule unchanged: dispatch through the caller's scope, the `next` guard, exact-type dispatch, keyed private registrations, validation, and disposal by the container.
5. Report each component's lifetime in `ArbitratorRegistrations`.

## Non-goals

- **Changing the lifetimes of `ISender` and `IPublisher`.** They stay scoped (decision 10).
- **Lifetimes chosen by attribute, by convention, or per `AddArbitrator` call** (decision 38).
- **A different lifetime per type within one scan** (decision 39). Filters split a scan.
- **Arbitrator-level validation of component dependencies.** Captive dependencies are container validation's job.
- **Making components thread-safe.** Scoped and singleton components must be written for concurrent use.
- **Pooling, lazy, or factory registrations.**

## Current state

- **Registration.**
  - Every Arbitrator descriptor is created by `ComponentDescriptors.Create(componentType, serviceKey)` with `ServiceLifetime.Transient`.
  - Handlers use the request-components or notification-handlers key. Behaviors use the request-components or notification-behaviors key.
  - Closed and open behavior registrations derive from the `closed` record `BehaviorRegistration`. Open behavior descriptors are emitted later, when a plan they apply to is created or the behavior is placed.
- **Plans.** `PipelinePlan` holds a `HandlerContract` and its planned behaviors. `NotificationPlan` holds handler types and planned behaviors. Both describe themselves for inspection.
- **Builder.** `ArbitratorBuilder` has eight public registration methods and is 245 lines, against the 250-line type limit.
- **Inspection.** `RequestRegistration` exposes `HandlerType` and `BehaviorTypes`. `NotificationRegistration` exposes `HandlerTypes` and `BehaviorTypes`. All are `Type` values.
- **Contract.** §5.1 lists every component as transient. Principle 8, §4.5, and rows 24 and 28 rely on transient instances.

## Proposed solution

1. **API.** Eight new builder overloads each take a `ServiceLifetime`:
   - `AddHandler`, `AddNotificationHandler`, `AddBehavior`, and `AddOpenBehavior`
   - the four scanning methods

   The existing overloads call them with `ServiceLifetime.Transient`.
2. **Recording the lifetime.**
   - The registry and `Registrations` methods take the lifetime and pass it on.
   - Handler lifetimes are stored with the handler in its plan.
   - Behavior lifetimes are stored on the behavior registration, so open behavior descriptors emitted later still use the right lifetime.
3. **Descriptors.** `ComponentDescriptors` takes a lifetime for every descriptor it creates.
4. **Inspection.** A new public `ComponentRegistration` pairs an implementation type with its lifetime. `RequestRegistration` and `NotificationRegistration` gain lists of these entries next to their existing type properties.
5. **Builder size.** `ArbitratorBuilder` becomes a partial class in three files by concern:
   - registering handlers
   - registering behaviors
   - scanning

   It also uses `inheritdoc` between overloads.

**Why this fits.** The lifetime is a single value carried from the builder to the descriptor. Dispatch, invokers, guards, and the provider check don't change, because they resolve components by type and key and let MS.DI apply the lifetime.

## Codebase impact

- **Library (`src/Arbitrator`).**
  - **Public:**
    - eight builder overloads
    - one new type, `ComponentRegistration`
    - four new properties: `RequestRegistration.Handler` and `Behaviors`, and `NotificationRegistration.Handlers` and `Behaviors`

    No existing public member changes.
  - **Internal:** the lifetime travels through the registry, `Registrations`, the inspectors, the plans, the behavior registrations, and `ComponentDescriptors`. Dispatch code is unchanged.
  - **Project file:** the version becomes `0.7.0` with suffix `preview.1`.
- **New library or package:** none. `ServiceLifetime` comes from `Microsoft.Extensions.DependencyInjection.Abstractions`, which ships with .NET 11 and which Arbitrator already uses.
- **Public API files:** 15 new entries in `PublicAPI.Unshipped.txt`.
- **Tests:** a new `Lifetimes/` suite, plus inspection additions.
- **Documentation:** `contract.md`, `architecture.md`, `README.md`, this design, and decisions 38–40, with decision 10's status updated.
- **Configuration, infrastructure, CI, persistence, migrations:** none.

## Target structure

Only new and changed paths are listed.

- `src/Arbitrator/`
  - `ArbitratorBuilder.cs`: changed. The class becomes `partial`. This file keeps:
    - the class documentation, the constructor, and the private helpers, including the new lifetime check
    - `AddHandler` and `AddNotificationHandler` with their lifetime overloads
  - `ArbitratorBuilder.Behaviors.cs`: new. `AddBehavior` and `AddOpenBehavior` with their lifetime overloads.
  - `ArbitratorBuilder.Scanning.cs`: new. The four scanning methods and their four lifetime overloads.
  - `ComponentRegistration.cs`: new. The public entry: implementation type and lifetime.
  - `RequestRegistration.cs`, `NotificationRegistration.cs`: changed. Gain the entry lists. The existing type properties are derived from them.
  - `Arbitrator.csproj`, `PublicAPI.Unshipped.txt`: changed.
  - `Registration/`
    - `ArbitratorRegistry.cs`: changed. Every registration method takes a lifetime and passes it on.
    - `Registrations.cs`: changed. Passes the lifetime to plans, behavior inspection, and descriptors.
    - `BehaviorRegistration.cs`: changed. Gains a required `Lifetime` property, set at construction. It stays a property rather than a positional parameter, so derived records keep at most four positional parameters.
    - `BehaviorTypeInspector.cs`: changed. Takes the lifetime and sets it on the registration it returns.
    - `RegisteredBehaviors.cs`: changed. `PlanPipeline` takes the handler's lifetime, and `PlanNotification` is unchanged.
    - `PipelinePlan.cs`: changed. Holds the handler's `ComponentRegistration`, and describes its handler and behaviors with lifetimes.
    - `NotificationPlan.cs`, `RegisteredNotificationHandlers.cs`: changed. Handlers are held as `ComponentRegistration` entries, in run order.
    - `PlannedBehaviors.cs`: changed. Lists its behaviors as `ComponentRegistration` entries, using each registration's lifetime.
    - `ComponentDescriptors.cs`: changed. Every factory takes the lifetime, from its caller or from the behavior registration. The request factory, now used only for handlers, is named `ForRequestHandler`.
- `tests/Arbitrator.Tests/`
  - `Lifetimes/LifetimeDispatchTests.cs`: new. Instance identity across sends, reads, publishes, and scopes, for each lifetime and component kind.
  - `Lifetimes/LifetimeRegistrationTests.cs`: new. The overloads, undefined values, roles, open behavior instantiations, copied collections, and container validation.
  - `Lifetimes/LifetimeScanningTests.cs`: new. Each scan overload's lifetime, and transient without one.
  - `Inspection/RequestInspectionTests.cs`, `Inspection/NotificationInspectionTests.cs`: changed. Cover lifetimes.
  - `Support/RecordingBehavior{TRequest,TResponse}.cs`, `Support/RecordingStreamBehavior{TRequest,TItem}.cs`: changed. They also capture themselves, as the handler and notification doubles already do, so identity tests can compare instances.
- `docs/`
  - `design/configurable-lifetimes.md`: new (this document).
  - `decisions/0038`–`0040`: new. `decisions/0010`: status changed.
  - `contract.md`, `architecture.md`: changed, as listed under "Rollout and rollback".
- `README.md`: changed.

## Detailed behavior

### 1. Public API

| Member | Contract |
| --- | --- |
| `AddHandler<THandler>(ServiceLifetime lifetime)` | As `AddHandler<THandler>()`, registering the handler with `lifetime` |
| `AddNotificationHandler<THandler>(ServiceLifetime lifetime)` | As `AddNotificationHandler<THandler>()`, with `lifetime` |
| `AddBehavior<TBehavior>(ServiceLifetime lifetime)` | As `AddBehavior<TBehavior>()`, with `lifetime` for every role the behavior has |
| `AddOpenBehavior(Type openBehaviorType, ServiceLifetime lifetime)` | As `AddOpenBehavior(Type)`, with `lifetime` for every closed instantiation |
| `AddHandlersFromAssembly(Assembly assembly, ServiceLifetime lifetime)` | As the scan without a filter, registering every handler it finds with `lifetime` |
| `AddHandlersFromAssembly(Assembly assembly, Func<Type, bool> filter, ServiceLifetime lifetime)` | As the filtered scan, with `lifetime` |
| `AddHandlersFromAssemblyContaining<TMarker>(ServiceLifetime lifetime)` | As the marker scan, with `lifetime` |
| `AddHandlersFromAssemblyContaining<TMarker>(Func<Type, bool> filter, ServiceLifetime lifetime)` | As the filtered marker scan, with `lifetime` |
| `ComponentRegistration` | Sealed class with no public constructor. Properties `ImplementationType` (`Type`) and `Lifetime` (`ServiceLifetime`) |
| `RequestRegistration.Handler` | The handler's `ComponentRegistration`. Its `ImplementationType` equals `HandlerType` |
| `RequestRegistration.Behaviors` | `IReadOnlyList<ComponentRegistration>`, in the same order as `BehaviorTypes` |
| `NotificationRegistration.Handlers` | `IReadOnlyList<ComponentRegistration>`, in the same order as `HandlerTypes` |
| `NotificationRegistration.Behaviors` | `IReadOnlyList<ComponentRegistration>`, in the same order as `BehaviorTypes` |

- **Annotations.** Each overload carries exactly the trimming annotations of the method it extends.
- **Documentation.** Each overload inherits its counterpart's documentation through `inheritdoc` and documents `lifetime`, including the captive-dependency warning for singletons.
- **No equality.** `ComponentRegistration` has no value equality and no `ToString` override, like the other inspection types.

### 2. Validation and order of checks

A builder call with a lifetime runs its checks in this order:

1. **Null arguments.** `ArgumentNullException`, as today.
2. **Undefined lifetime.** A value that isn't a defined `ServiceLifetime` member throws `ArgumentOutOfRangeException` for parameter `lifetime`.
3. Every existing check, unchanged: read-only collection, scanning failures, frozen registry, and per-type validation.

Nothing is registered or added when any check fails, as for every builder call.

### 3. Lifetime semantics

- **Transient.** A new instance every time dispatch resolves the component: per send, per read of a stream, and per handler call in a publish. This is today's behavior.
- **Scoped.** One instance per scope, shared by every send, read, and publish in that scope, including concurrent ones and nested dispatch.
- **Singleton.** One instance per root provider, shared across all scopes and threads. MS.DI resolves it from the root, even when dispatch asks the caller's scope for it.
- **Roles.** A class has one lifetime per role.
  - A class registered as a request handler and as a notification handler can have different lifetimes in each role, and each role has its own instance.
  - A handler class implementing several request contracts has one lifetime and one descriptor, so under scoped or singleton every contract shares one instance.
  - A closed behavior implementing request and notification contracts gets one lifetime for both of its descriptors. Each descriptor has its own instance.
- **Open behaviors.** The lifetime applies to every closed instantiation. Each instantiation is a separate service, so `LoggingBehavior<A, R>` and `LoggingBehavior<B, S>` are different singletons.
- **Notification behaviors.** Decision 37's "each handler call gets its own behavior instances" holds for transient behaviors. A scoped or singleton notification behavior is the same instance around every handler call of a publish.
- **Resolution timing.** Unchanged: every component a dispatch needs is resolved before any runs (decision 13). A scoped or singleton component is only constructed on its first resolution.
- **Guards.** Unchanged. The continuation guard is per dispatch, not per instance, so a singleton behavior still gets a fresh guarded `next` every time.
- **Disposal.** Unchanged: Arbitrator disposes no services. Scoped components are disposed with their scope, and singletons with the root provider.
- **Captive dependencies.** Container validation (`ValidateScopes` with `ValidateOnBuild`) reports a singleton component that depends on a scoped service, including Arbitrator's keyed registrations. Without validation, the singleton keeps the first scoped instance it received for the application's lifetime, as any MS.DI singleton does.
- **Root resolution.** If `ISender` is resolved from the root provider with scope validation off, scoped components are resolved from the root and behave as singletons, as `contract.md` §5.2 already describes for scoped dependencies.

### 4. Scanning

- A scan overload registers every type it accepts, whether a request, stream, or notification handler, with the given lifetime.
- The existing scan overloads register transient components.
- Handlers needing different lifetimes are split across scan calls with filters, or excluded and registered explicitly.

### 5. Inspection

- `RequestRegistration.Handler.Lifetime` is the handler's lifetime in its request role.
- `RequestRegistration.Behaviors[i].Lifetime` is the lifetime of the behavior at `BehaviorTypes[i]`. For an open behavior, that is its registration's lifetime.
- `NotificationRegistration.Handlers[i]` and `Behaviors[i]` work the same way, in run order and outermost-first order.
- The lists are immutable, like the existing ones.

### 6. Edge cases

| Case | Outcome |
| --- | --- |
| `AddHandler<T>(ServiceLifetime.Transient)` | Identical to `AddHandler<T>()` |
| `(ServiceLifetime)42` passed as a lifetime | `ArgumentOutOfRangeException` for `lifetime`, and nothing is registered |
| Same handler type registered twice with different lifetimes | The existing duplicate error. One registration per role |
| Singleton handler with a scoped dependency, validation on | `ValidateOnBuild` reports it when the provider is built |
| Singleton handler with a scoped dependency, validation off | Resolves. The dependency is captured for the application's lifetime |
| Scoped behavior around a transient handler | Allowed. Both are resolved from the caller's scope |
| Singleton behavior that keeps state between before and after `next` | That state is shared by concurrent dispatches, which is the application's responsibility |
| Copied collection | The copy keeps the lifetimes recorded so far, and later registrations on either collection don't affect the other |
| Scanning with a lifetime after an open behavior was registered | The scanned handlers get the scan's lifetime, and the behavior keeps its own |

### 7. Behavioral contract additions (`contract.md` §6)

New rows 73 to 81:

- **73 Lifetime overloads.** Each component is registered with the chosen lifetime. Without one, it's transient.
- **74 Scoped components.** One instance per scope, shared by all dispatches in it, including concurrent ones.
- **75 Singleton components.** One instance per root provider, shared across scopes. It must tolerate concurrent use.
- **76 Captive dependencies.** Container validation reports a singleton component with a scoped dependency. Without validation, MS.DI's own behavior applies.
- **77 Roles.** A class registered in two roles can have a different lifetime and its own instance in each.
- **78 Open behaviors.** The lifetime applies to every closed instantiation.
- **79 Scans.** Every type a scan with a lifetime registers gets that lifetime.
- **80 Undefined lifetime.** `ArgumentOutOfRangeException` for `lifetime`. Nothing is registered.
- **81 Inspection.** Every component entry reports its lifetime.

Changed rows:
- **Row 24**, concurrent sends in one scope: transient components are distinct instances, while scoped and singleton ones are shared.
- **Row 28**, one class with several handler contracts: a new instance per send applies to transient handlers.
- **Row 70**, notification behavior instances: per handler call applies to transient behaviors.

## Dependencies

No dependency is added, updated, replaced or removed by this feature.

| Dependency | Change | Audit result |
| --- | --- | --- |
| None | n/a | `dotnet list package --vulnerable --include-transitive` against nuget.org on 2026-11-12 reported no vulnerable packages in `Arbitrator` or `Arbitrator.Tests`, at the package versions in the working tree |

## Security considerations

- **Shared state across requests.** A scoped or singleton component holding per-request data leaks it between sends in a scope, or between users across scopes. This is the main risk the feature introduces.
  - Transient stays the default.
  - The XML documentation and the README warn against request-specific state in scoped and singleton components.
  - The contract states that Arbitrator doesn't make shared components safe.
- **Captive dependencies.** A singleton capturing a scoped `DbContext` or user context shares one user's context with every later request. Container validation reports it. The README tells applications to enable validation, at least in development and in a startup test, as `contract.md` already recommends.
- **No new input surface.** Lifetimes come from application code at startup. Undefined enum values are rejected.
- **No data in messages.** Unchanged. Messages name types only.

## Testing strategy

All tests are TUnit tests through the public API against MS.DI, using `TestHost` and `Journal`. Identity assertions compare captured instances. Order assertions use `CollectionOrdering.Matching`. None uses timing.

- **Dispatch identity.** For each lifetime:
  - two sends in one scope, and one send in each of two scopes, for a request handler, a request behavior, a stream handler, a stream behavior, a notification handler, and a notification behavior
  - a notification behavior around two handlers in one publish

  Transient gives distinct instances everywhere. Scoped gives the same instance within a scope and different ones across scopes. Singleton gives the same instance everywhere.
- **Registration.**
  - Each overload, and the existing overloads registering transient.
  - An undefined lifetime value, with nothing registered.
  - Different lifetimes for one class in two roles.
  - An open behavior's lifetime on two instantiations.
  - A closed behavior with request and notification roles.
  - Duplicates with different lifetimes.
  - A copied collection.
  - The frozen and read-only checks with the new overloads.
- **Scanning.** Each scan overload's lifetime on request and notification handlers. The existing overloads stay transient.
- **Container validation.** A singleton handler and a singleton behavior with a scoped dependency are reported by `ValidateOnBuild`.
- **Inspection.** `Handler`, `Behaviors`, and `Handlers` report lifetimes, in the same order as the type lists.
- **Regression.** The existing 265 tests pass unchanged.
- **Analyzers.** The Release build has zero warnings, including public API, trimming and AOT analyzers.

## Rollout and rollback

- **Release.** The work merges to `main` through a pull request that must pass "Build and test". It ships as `0.7.0-preview.1`. The release notes state that the feature is additive and transient stays the default.
- **Documentation updates in the same change.**
  - `contract.md`:
    - §1: principle 8's "transient-only lifetimes" becomes "transient components by default".
    - §3.2 lists the lifetime overloads.
    - §4.4: scans can take a lifetime.
    - §4.5: concurrency covers shared components.
    - §5.1: the lifetime table says "Transient by default, or as registered".
    - §5.3: disposal of scoped and singleton components.
    - §5.5: container validation covers captive singletons.
    - §5.6: inspection entries.
    - §6 gains rows 73–81, and rows 24, 28, and 70 are updated.
    - §7.1 adds `ArgumentOutOfRangeException`.
  - `architecture.md`: registration carries the lifetime, and descriptor creation and the inspection description are updated.
  - `README.md`: a "Lifetimes" section, before "Testing your configuration", covers the overloads, scan lifetimes, the shared-state warning, and validation. "The contract in brief" lifetime row is updated.
  - `docs/design/configurable-lifetimes.md`: the status becomes "Implemented". Issue #8 is closed after the merge.
- **Migration:** none. Existing registrations keep their transient lifetime.
- **Feature flag:** none.
- **Rollback.** Consumers pin `0.6.0-preview.1`.

## Risks and mitigations

| Risk | Cause | Impact | Detection | Mitigation |
| --- | --- | --- | --- | --- |
| Request data leaks across sends or users | A scoped or singleton component keeps per-request state | Data from one request seen in another | Code review, and application tests | Transient default, warnings in XML documentation, README, and contract |
| Captive scoped dependency in a singleton | A singleton handler depends on a scoped service | One scoped instance shared for the application's lifetime | Container validation | README and contract recommend validation. A test proves keyed singletons are reported |
| Concurrency bugs in shared components | Concurrent sends share scoped or singleton instances | Races inside application components | Application tests | Documented in the contract (§4.5) and XML documentation |
| Wrong lifetime on later-emitted open behavior descriptors | The lifetime isn't carried to descriptors emitted after registration | A behavior silently transient | Registration and dispatch tests that register handlers after behaviors | The lifetime is stored on the behavior registration and read whenever a descriptor is emitted |
| `ArbitratorBuilder` exceeds the 250-line type limit | Eight more documented public overloads | A maintainability standard is breached | Measuring during implementation | Split into three partial files by concern, each under 250 lines, with `inheritdoc` between overloads. The type as a whole exceeds the limit because its size is public API documentation, not logic, and this is reported as an exception |
| Registry and `Registrations` signatures grow | A lifetime parameter on every method | Methods near the four-parameter limit | Measuring during implementation | Each method gains one parameter at most. Where a fifth would be needed, the lifetime travels with the registration record |

## Pros and cons

**Pros**

- Expensive components can be singletons, and scope-wide state becomes possible, without leaving Arbitrator's registration model.
- No change for existing applications: transient remains the default.
- MS.DI applies the lifetimes and validates them, and Arbitrator adds no lifetime rules.
- Tests can assert lifetimes through inspection.

**Cons**

- Scoped and singleton components open the door to shared-state and captive-dependency bugs that transient-only registration prevented.
- Eight more public overloads, and a builder split across files.
- Some instance guarantees, such as a new instance per send and decision 37's instances per handler call, now hold only for transient components.

**Alternatives considered**

- **Attribute or per-call defaults.** Rejected in decision 38.
- **Transient and scoped only.** Rejected in decision 38.
- **Scans that stay transient.** Rejected in decision 39.
- **Parallel lifetime lists in inspection.** Rejected in decision 40.

## Acceptance criteria

Each criterion has at least one test, in `tests/Arbitrator.Tests/Lifetimes/` unless another place is named.

- **CL1** Each of the eight overloads registers its components with the given lifetime. The existing overloads register transient components.
- **CL2** Transient, scoped, and singleton request handlers, request behaviors, stream handlers, stream behaviors, notification handlers, and notification behaviors have the instance identity of §3, within and across scopes.
- **CL3** A scoped or singleton notification behavior is the same instance around every handler call of a publish. A transient one is distinct per handler call.
- **CL4** A class registered in two roles keeps a separate lifetime and instance per role. An open behavior's lifetime applies to every instantiation. A closed behavior with request and notification roles uses its lifetime for both.
- **CL5** A scan overload registers every request, stream, and notification handler it accepts with its lifetime.
- **CL6** An undefined lifetime value throws `ArgumentOutOfRangeException` for `lifetime`, and nothing is registered.
- **CL7** `ValidateOnBuild` reports a singleton handler or behavior that depends on a scoped service.
- **CL8** A copied collection keeps lifetimes, and the existing frozen, read-only, and duplicate checks apply to the new overloads.
- **CL9** `ArbitratorRegistrations` reports every component's lifetime through `Handler`, `Handlers`, and `Behaviors`, in the same order as the type lists. This is tested in `tests/Arbitrator.Tests/Inspection/`.
- **CL10** The existing tests pass unchanged. The Release build has zero warnings, and the public API files list exactly the new members. Each partial file of `ArbitratorBuilder` is under 250 lines.
- **CL11** `contract.md`, `architecture.md`, and `README.md` contain the updates listed under "Rollout and rollback". The version is `0.7.0-preview.1`.
