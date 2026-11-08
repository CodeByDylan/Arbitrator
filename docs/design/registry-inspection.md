# Design: Registry inspection (v0.5.0)

| | |
| --- | --- |
| Status | Implemented |
| Release | 0.5.0 (published as `0.5.0-preview.1` until .NET 11 is generally available) |
| Date | 2026-11-08 |
| Branch | `feature/registry-inspection`, from `main` at `7e18527` |
| Issue | [#9 Registry inspection API](https://github.com/CodeByDylan/Arbitrator/issues/9) |
| Contract reference | [`contract.md`](../contract.md). This document names every section of it that changes |
| Decisions | [32](../decisions/0032-inspection-through-a-provider-singleton.md), [33](../decisions/0033-inspection-contents.md), [34](../decisions/0034-no-assembly-check-helper.md) |

## Summary

Arbitrator validates everything it can at registration and at the first sender resolution. One thing it can never know is whether every request type the application defines has a handler (`contract.md` §5.5): a request without a handler fails only when it's sent. Applications want to close that gap with a test, but today they can't see what Arbitrator registered.

v0.5.0 adds **registry inspection**. `AddArbitrator` registers a public singleton, `ArbitratorRegistrations`, describing what a provider dispatches:

- every request type, with its response or item type, whether it's a stream, its handler type, and the behaviors that run for it, outermost first
- every notification type, with its handler types in run order

An application resolves it in a test, compares it with the request types its assemblies define, and fails the build when one has no handler. The README shows that test. Inspection is read-only and additive: it changes no existing behavior.

## Goals

1. Let an application read, from a built provider, which request and notification types Arbitrator dispatches, and to which handlers.
2. Expose each request's pipeline: the closed behavior types that run for it, in order.
3. Describe exactly what that provider dispatches, after the same validation that sending would trigger.
4. Make the description deterministic, immutable, and safe to read concurrently.
5. Keep the change additive: no existing public member or behavior changes.

## Non-goals

- **A built-in check for unhandled request types** (decision 34). The README shows the test instead.
- **Reading registrations before a provider exists,** for example from the `IServiceCollection` (decision 32).
- **Changing registrations through inspection.** The description is read-only.
- **Describing services Arbitrator doesn't own,** such as handler dependencies or lifetimes, which are always transient.
- **Listing registered behaviors that apply to no request.** Open behaviors that match nothing are invisible, as they are at dispatch. Closed behaviors that match nothing already fail resolution.
- **Diagnostics output,** such as logging the registrations or a formatted dump.
- **Native AOT support.**

## Current state

- **Registration** (`src/Arbitrator/Registration/`). `ArbitratorRegistry` holds `Registrations`:
  - `RegisteredPipelines` maps each request type to a `PipelinePlan`. The plan holds its `HandlerContract` (request type, response or item type, pipeline kind, handler type) and its planned behaviors, in global order, each with the closed type that runs.
  - `RegisteredNotificationHandlers` maps each notification type to a `NotificationPlan`, which holds its handler types in registration order privately.
- **Freezing.** The `DispatchTable` singleton factory calls `ArbitratorRegistry.Freeze()`. It freezes the registry, checks that every closed behavior applies, and returns `DispatchMaps`, cached for every provider built from the collection. The table then checks that the provider can resolve every component.
- **Core services.** The first `AddArbitrator` call adds four descriptors: the registry, the dispatch table, `ISender`, and `IPublisher`.
- **No public read access.** All of the above is internal. `contract.md` §5.5 lists "Arbitrator can't prove that every request type in the application has a handler" under "Never".

## Proposed solution

At freeze time, describe the frozen registrations as an immutable `ArbitratorRegistrations` object, alongside the dispatch maps, and expose it as a singleton that resolves through the dispatch table.

1. **Public types** (namespace `Arbitrator`): `ArbitratorRegistrations`, `RequestRegistration`, and `NotificationRegistration`. They are sealed classes with internal constructors and read-only properties.
2. **Description.** `Registrations.CreateDispatchMaps()` also builds the description from the same plans. `RegisteredPipelines` and `RegisteredNotificationHandlers` each describe their own entries. `DispatchMaps` carries the description, and `DispatchTable` exposes it.
3. **Service.** The first `AddArbitrator` call adds a fifth core descriptor: a singleton `ArbitratorRegistrations` whose factory resolves the `DispatchTable` and returns its description. Resolving inspection therefore performs, and fails, exactly like the first `ISender` resolution.

**Why this fits.**
- **One source of truth.** The description and the invokers come from the same frozen plans in the same call, so they can't disagree.
- **No new validation path.** Inspection reuses the freeze, the closed-behavior check, and the provider check.
- **Cheap.** The description is built once per service collection and shared by every provider built from it, like `DispatchMaps`.

## Codebase impact

- **Library (`src/Arbitrator`).**
  - **Public:** three new types, with their properties. No existing public member changes.
  - **Internal:**
    - Describe methods on `RegisteredPipelines` and `RegisteredNotificationHandlers`.
    - A `Describe` method on `PipelinePlan` and `NotificationPlan`, each returning its own description.
    - A description member on `DispatchMaps` and `DispatchTable`.
    - One more core descriptor in `ArbitratorServiceCollectionExtensions`.
  - **Project file:** the version becomes `0.5.0` with suffix `preview.1`.
- **New library or package:** none. Inspection reads Arbitrator's internal state, so it must live in the same assembly. Decision 18 (a single package) stands.
- **Public API files:** the new members are added to `PublicAPI.Unshipped.txt`.
- **Tests:** a new `Inspection/` suite and one example test.
- **Documentation:** `contract.md`, `architecture.md`, `README.md`, this design, and decisions 32–34.
- **Configuration, infrastructure, CI, persistence, migrations:** none.

## Target structure

Only new and changed paths are listed.

- `src/Arbitrator/`
  - `ArbitratorRegistrations.cs`: new. The public description: `Requests` and `Notifications`.
  - `RequestRegistration.cs`: new. One request type's handler and pipeline.
  - `NotificationRegistration.cs`: new. One notification type's handlers.
  - `ArbitratorServiceCollectionExtensions.cs`: changed. Adds the singleton descriptor for `ArbitratorRegistrations`.
  - `Arbitrator.csproj`: changed. The version becomes `0.5.0-preview.1`.
  - `PublicAPI.Unshipped.txt`: changed. The new public API.
  - `Registration/`
    - `Registrations.cs`: changed. `CreateDispatchMaps` also creates the description.
    - `RegisteredPipelines.cs`: changed. Describes every request type's contract and planned behaviors.
    - `RegisteredNotificationHandlers.cs`: changed. Describes every notification type's handlers.
    - `PipelinePlan.cs`, `NotificationPlan.cs`: changed. Each describes itself. `PipelinePlan` shares the list of closed behavior types between its invoker and its description.
  - `Dispatch/`
    - `DispatchMaps.cs`: changed. Carries the description.
    - `DispatchTable.cs`: changed. Exposes the description.
- `tests/Arbitrator.Tests/`
  - `Inspection/`: new.
    - `RequestInspectionTests.cs`: request entries, response types, streams, behaviors, order.
    - `NotificationInspectionTests.cs`: notification entries and handler order.
    - `InspectionLifecycleTests.cs`: resolution, singleton identity, freezing, failures, immutability, scanning, copied collections, providers that share a collection.
  - `Examples/ExampleTests.cs`: changed. Adds the README's "every request type has a handler" test.
- `docs/`
  - `design/registry-inspection.md`: new (this document).
  - `decisions/0032`–`0034`: new.
  - `contract.md`, `architecture.md`: changed, as listed under "Rollout and rollback".
- `README.md`: changed, as listed under "Rollout and rollback".

## Detailed behavior

### 1. Public API

| Member | Contract |
| --- | --- |
| `ArbitratorRegistrations` | Sealed class with no public constructor. Describes what one service provider dispatches |
| `ArbitratorRegistrations.Requests` | `IReadOnlyList<RequestRegistration>`: one entry per registered request type, including stream request types |
| `ArbitratorRegistrations.Notifications` | `IReadOnlyList<NotificationRegistration>`: one entry per notification type that has at least one handler |
| `RequestRegistration` | Sealed class with no public constructor |
| `RequestRegistration.RequestType` | The exact request type that dispatch selects this entry for |
| `RequestRegistration.ResponseType` | The `TResponse` of `IRequest<TResponse>`. `Unit` for a request implementing `IRequest`. The `TItem` of `IStreamRequest<TItem>` for a stream request |
| `RequestRegistration.IsStream` | `true` for a request implementing `IStreamRequest<TItem>`, `false` otherwise |
| `RequestRegistration.HandlerType` | The handler implementation type |
| `RequestRegistration.BehaviorTypes` | `IReadOnlyList<Type>`: the closed behavior types that run for this request, outermost first. Open behaviors appear as the instantiation that runs, for example `LoggingBehavior<GetCustomer, CustomerDto?>`. Empty when none applies |
| `NotificationRegistration` | Sealed class with no public constructor |
| `NotificationRegistration.NotificationType` | The exact notification type |
| `NotificationRegistration.HandlerTypes` | `IReadOnlyList<Type>`: the handler implementation types, in the order they run |

- **Namespace.** `Arbitrator`, beside the other contracts.
- **No public constructors.** Applications only receive these objects. That keeps their invariants (sorted, complete, immutable) under Arbitrator's control.
- **No value equality, and no `ToString` override.** Instances compare by reference. Tests assert on the types they contain.
- **Documentation.** Every type and member has XML documentation, including the sort order, the meaning of `ResponseType` for each kind, and the freezing behavior of resolving the service.
- **Annotations.** None needed. The types only hold `Type` objects that registration already inspected.

### 2. Resolving the service

- **Registration.** The first `AddArbitrator` call on a service collection adds an unkeyed singleton descriptor for `ArbitratorRegistrations`, with a factory. Later calls add nothing.
- **Resolution.** The factory resolves the provider's `DispatchTable` and returns its description. The first resolution of either `ArbitratorRegistrations`, `ISender`, or `IPublisher` in a provider therefore:
  - freezes the registrations, so later builder calls throw the existing frozen-registry error
  - fails with the existing `ArbitratorConfigurationException` when a closed behavior matches no handler, or when components were registered after the provider was built
- **Repeated failure.** After a failure, every later resolution of any of the three fails the same way, because the dispatch table factory throws again.
- **Scopes.** As a singleton it resolves from the root provider and from any scope, including with `ValidateScopes` enabled, and returns the same instance.
- **Container validation.** `ValidateOnBuild` doesn't run the factory, so building a provider never freezes the registrations.
- **Several providers.** Providers built from one collection share the frozen registrations and their description, so each returns the same instance.
- **Copied collections.** A copied collection adopts its own registry on its first `AddArbitrator` call (`contract.md` §5.4), and its description reflects only its own registrations.

### 3. Contents and order

- **Requests.** One entry per request type with a handler, from explicit registration or scanning. Request types without a handler never appear.
- **Behaviors.** `BehaviorTypes` lists exactly the components dispatch resolves around the handler, in the same order: applicable open and closed behaviors in global registration order, filtered by kind and by generic constraints. Request behaviors never appear on a stream request's entry, and the reverse.
- **Notifications.** One entry per notification type with at least one handler. `HandlerTypes` lists handlers in run order, which is registration order, including scanned handlers in their scan position.
- **Sort order.** Both lists are sorted by the ordinal order of `Type.FullName`. Within an entry, behavior and handler types keep dispatch order. The order is the same in every run, on every machine, for the same registrations.
- **Immutability.** The lists are immutable. Changing them through a mutable collection interface, such as `IList<T>`, throws `NotSupportedException`.
- **No activation.** Inspection never constructs a handler, behavior, or any other service.
- **Consistency.** The description is built in the same call as the dispatch invokers, from the same frozen plans. For every entry, dispatch resolves exactly `BehaviorTypes`, then `HandlerType`.

### 4. Edge cases

| Case | Outcome |
| --- | --- |
| `AddArbitrator` called with no registrations | Both lists are empty |
| A request implementing `IRequest` | `ResponseType` is `Unit`, and behaviors appear closed over `Unit` |
| One handler class handling several request types | One entry per request type, each with the same `HandlerType` |
| One class registered as a request handler and a notification handler | It appears in both lists |
| A notification handler class handling several notification types | It appears in each notification's `HandlerTypes` |
| A struct request type | Listed like any other request type |
| A derived request type without its own handler | Not listed. Only the registered base type is |
| An open behavior whose constraints match no registered request | Appears in no entry |
| A provider that doesn't implement `IServiceProviderIsKeyedService` | Inspection works. The provider check is skipped, as for `ISender` |
| Resolution after registrations were added following `BuildServiceProvider` | Fails with the existing "registered after this service provider was built" error |

### 5. Behavioral contract additions (`contract.md` §6)

New rows 57 to 62:

- **57 Inspection service.** `ArbitratorRegistrations` resolves as a singleton, from the root provider or any scope, and describes what that provider dispatches.
- **58 Freezing.** Resolving it freezes the registrations and runs the checks of the first `ISender` or `IPublisher` resolution, failing with the same exceptions.
- **59 Requests.** One entry per request type with a handler: request type, response or item type (`Unit` for `IRequest`), stream flag, handler type, and the closed behavior types that run, outermost first.
- **60 Notifications.** One entry per notification type with handlers, listing them in run order.
- **61 Order.** Both lists are sorted by ordinal full type name.
- **62 Read-only.** The description is immutable, and inspecting it constructs nothing.

## Dependencies

No dependency is added, updated, replaced or removed. The implementation uses `System.Collections.Immutable` and `System.Linq` from the .NET 11 shared framework.

| Dependency | Change | Audit result |
| --- | --- | --- |
| None | n/a | `dotnet list package --vulnerable --include-transitive` against nuget.org on 2026-11-08 reported no vulnerable packages in `Arbitrator` or `Arbitrator.Tests` |

## Security considerations

- **No new exposure.** The description is an in-process object, reachable only by code that already holds the service provider. It contains `Type` objects, which that code can already reflect over. It contains no request data, configuration values, or secrets.
- **Don't publish it.** Type names reveal an application's internal structure, as Arbitrator's exception messages do (`contract.md` §7.3). The README states that the description is for tests and diagnostics, and must not be returned from endpoints.
- **No mutation surface.** No public constructors, no setters, and immutable lists, so application code can't alter what other code reads. Dispatch never reads the description, so even reflection-based tampering couldn't change dispatch.
- **No denial-of-service surface.** It's built once per service collection, in time and memory linear in the number of registrations. Reading it allocates nothing.
- **No activation.** Inspection constructs no application services, so it can't trigger their side effects.

## Testing strategy

All tests are TUnit tests through the public API against MS.DI, using `TestHost` and the existing support types. None uses timing.

- **Requests.** Entries for:
  - a request with a response
  - a no-response request, with `ResponseType` `Unit`
  - a stream request, with `IsStream` set and the item type
  - a struct request
  - a handler class with several contracts

  Behavior lists cover open, closed, and constrained behaviors, interleaved across two `AddArbitrator` calls, in dispatch order. Request and stream behaviors stay on their own kind. A test journals the behaviors a send actually runs and compares them with `BehaviorTypes`.
- **Notifications.** Several handlers per notification in registration order, a handler class for several notifications, and a class that is both a request and a notification handler.
- **Order.** Types registered in reverse name order come out sorted by ordinal full name.
- **Lifecycle.**
  - It resolves from the root with `ValidateScopes` and from a scope, as the same instance.
  - Resolving it freezes the registrations: a later builder call throws the frozen error.
  - It fails like `ISender` for a closed behavior without a handler, and for registrations made after the provider was built.
  - Building with `ValidateOnBuild` doesn't freeze.
  - Two providers from one collection return the same description.
  - Copied collections describe only their own registrations.
  - Scanned handlers appear.
  - With no registrations, both lists are empty.
- **Immutability.** Changing a list through `IList<T>` throws `NotSupportedException`, and inspecting constructs no handler. A handler records its construction in the journal, and the journal stays empty.
- **Example.** The README test runs in `ExampleTests`: every example request type defined in the test assembly's `Examples` namespace has a handler.
- **Regression.** The existing 214 tests pass unchanged.
- **Analyzers.** The Release build has zero warnings, including public API, trimming and AOT analyzers.
- **Performance tests:** not needed. The description is built once per service collection.

## Rollout and rollback

- **Release.** The work merges to `main` through a pull request that must pass "Build and test". It ships as `0.5.0-preview.1` through `docs/releasing.md`. The release notes state that the change is additive.
- **Documentation updates in the same change.**
  - `contract.md`:
    - §3.1 lists the three new types.
    - §5.1 adds `ArbitratorRegistrations` as an unkeyed singleton.
    - A new §5.6, "Inspecting registrations", summarizes Detailed behavior §2–§3.
    - §5.5: the "Never" row says that applications can check handler coverage themselves with inspection, and the recommended application-side test includes that check.
    - §6 gains rows 57–62.
  - `architecture.md`:
    - The core descriptors become five.
    - The dispatch table section says the description is built with the invokers.
    - The state table lists it.
  - `README.md`: a new "Testing your configuration" section before "The contract in brief". It covers:
    - resolving `ArbitratorRegistrations` from the built host
    - the handler coverage test
    - asserting a request's behaviors
    - the freezing note
    - the warning against exposing it
  - `docs/design/registry-inspection.md`: the status becomes "Implemented". Issue #9 is closed after the merge.
- **Migration:** none. Applications that never resolve the service see no change, except one more descriptor in their service collection.
- **Feature flag:** none needed. The service is inert until resolved.
- **Rollback.** Consumers pin `0.4.0-preview.1`. A defective preview is unlisted and replaced by a higher version.

## Risks and mitigations

| Risk | Cause | Impact | Detection | Mitigation |
| --- | --- | --- | --- | --- |
| Description and dispatch disagree | They're built from different data or at different times | Tests pass while dispatch behaves differently | A test comparing journaled behaviors with `BehaviorTypes` | Both come from the same frozen plans in one call |
| Inspection unexpectedly freezes registrations | A test resolves it before finishing configuration | Later builder calls throw | The existing frozen error, which names the cause | Documented in XML docs, contract §5.6, README, and decision 32 |
| Applications rely on order or contents that later change | The lists become de facto contract | A future change breaks tests | Contract rows 59–61 | The order and contents are specified, and changes go through the contract |
| The description is exposed over HTTP | A developer returns it from a diagnostics endpoint | Internal structure disclosed | Code review | README warning, same stance as exception messages |
| The extra core descriptor affects tests that count descriptors | One more descriptor after the first `AddArbitrator` | Brittle application tests break | Their own test failures | Listed in the release notes |
| `Registrations` grows past the 250-line limit | New describe logic | A maintainability standard is breached | Measuring during implementation | Describing lives in `RegisteredPipelines` and `RegisteredNotificationHandlers`, so `Registrations` only combines it |

## Pros and cons

**Pros**

- Applications can finally prove handler coverage, and can assert pipeline composition, in ordinary tests.
- It describes exactly what dispatch uses, after the same validation, with no second source of truth.
- Additive and inert until used, with no new dependency or package.
- Deterministic and immutable, so it's safe in parallel tests.

**Cons**

- Resolving it freezes registrations, which can surprise code that inspects mid-configuration.
- Behavior order becomes visible API, so changing how pipelines are composed would show in tests.
- The coverage check is left to each application.

**Alternatives considered**

- **A snapshot from the `IServiceCollection`, or both access points.** Rejected in decision 32.
- **Handlers only.** Rejected in decision 33, so that pipelines can be asserted.
- **An interface, or records.** Rejected in decision 33.
- **A built-in coverage check.** Rejected in decision 34.
- **Building the description lazily on first read.** Rejected: it would need its own synchronization, and the cost of building it eagerly at freeze time is negligible.

## Acceptance criteria

Each criterion has at least one test in `tests/Arbitrator.Tests/Inspection/`, unless it names another place.

- **RI1** `ArbitratorRegistrations` resolves from the root provider with `ValidateScopes` enabled and from a scope, as the same instance.
- **RI2** Each registered request type has exactly one entry, with the right request, response or item, stream flag, and handler type. A no-response request has `ResponseType` `Unit`.
- **RI3** `BehaviorTypes` equals, in order, the behaviors a send or read of that request actually runs. This covers open, closed, constrained, and cross-call registrations, with request and stream behaviors kept to their own kind.
- **RI4** Each notification type with handlers has exactly one entry, listing its handlers in run order.
- **RI5** Both lists are sorted by ordinal full type name.
- **RI6** Resolving the service freezes the registrations. It fails with the same exception as `ISender` for a closed behavior without a handler, and for registrations made after the provider was built.
- **RI7** Building a provider with `ValidateOnBuild` doesn't freeze the registrations.
- **RI8** Scanned handlers appear. A copied collection describes only its own registrations. With no registrations, both lists are empty.
- **RI9** The lists are immutable, and inspecting constructs no handler or behavior.
- **RI10** The README's coverage test runs in `ExampleTests`.
- **RI11** The existing tests pass unchanged. The Release build has zero warnings, and the public API files list exactly the new members.
- **RI12** `contract.md`, `architecture.md` and `README.md` contain the updates listed under "Rollout and rollback". The package version is `0.5.0-preview.1`.
