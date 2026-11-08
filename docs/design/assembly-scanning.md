# Design: Assembly scanning (v0.3.0)

| | |
| --- | --- |
| Status | Implemented |
| Release | 0.3.0 (published as `0.3.0-preview.1` until .NET 11 is generally available) |
| Date | 2026-11-08 |
| Branch | `feature/assembly-scanning`, from `main` |
| Contract reference | `DESIGN.md`. This document extends it and names every section of it that changes |
| Note | `DESIGN.md` has since been split up ([decision 24](../decisions/0024-documentation-layout.md)). Sections that existed in v0.1 are in [`requests-and-pipeline.md`](requests-and-pipeline.md). Sections added or changed later now live in [`contract.md`](../contract.md), and the git history holds the last version of `DESIGN.md` |

## Summary

Arbitrator registers every handler explicitly: one `AddHandler<THandler>()` or `AddNotificationHandler<THandler>()` call per type. An application with dozens of handlers keeps a long registration list. The list must grow with every new handler, and a handler that is written but never added fails only at dispatch.

v0.3.0 adds **opt-in assembly scanning** for handlers. The builder gains these methods:

- `AddHandlersFromAssembly(Assembly)` and `AddHandlersFromAssemblyContaining<TMarker>()`.
- An overload of each that takes a type filter.

A scan registers every request handler and notification handler class that the named assembly defines. Each discovered type goes through the same validation as an explicit registration. Behaviors are never discovered: their order defines the pipeline, so they stay explicit.

A scan is deterministic: discovered types are processed in ordinal order of their full names. It is atomic: it registers everything it found, or nothing. It is strict: an already-registered type, an invalid handler, an assembly with no handlers, and an assembly whose types can't be loaded are all configuration errors.

## Goals

1. Register every request handler and notification handler of an assembly with one builder call.
2. Keep the existing registration rules unchanged for discovered types: validation, duplicates, transient lifetimes, keyed registration, freezing, copied-collection isolation, and the provider check.
3. Make the result independent of compiler, build and machine: the same assembly always produces the same registrations in the same order.
4. Let the caller leave out discovered types with a filter, so scanned and explicit registrations can be combined without duplicates.
5. Report a scan that registers nothing, or that can't read the assembly, as a configuration error that names the assembly.
6. Keep the change additive: no existing public member changes, and no existing behavior changes.

## Non-goals

- **Discovering behaviors.** Closed and open behaviors are registered only with `AddBehavior` and `AddOpenBehavior`.
- **Scanning more than one assembly per call, or every loaded assembly.** Each call names exactly one assembly, and multiple assemblies need one call each.
- **Lenient duplicates.** A scan never skips a type because it is already registered.
- **Filtering by namespace, attribute or naming convention through dedicated API.** The single type filter covers these cases.
- **Configurable lifetimes, ordering attributes or priorities for discovered types.**
- **A source generator or Native AOT support for scanning.** Scanning is reflection-based and carries the trimming and dynamic-code warnings.
- **Scanning as a default.** `AddArbitrator` never scans unless a scanning method is called.

## Current state

- **Registration.** `ArbitratorBuilder` exposes four registration methods.
  - `AddHandler<THandler>()`, `AddBehavior<TBehavior>()` and `AddOpenBehavior(Type)` handle requests and behaviors.
  - `AddNotificationHandler<THandler>()` handles notification handlers.
- **Validation and recording.** Each method validates a single type through `ArbitratorRegistry`, which holds one `Registrations` instance behind a lock. The builder then adds the resulting keyed descriptors to the service collection.
- **Atomicity.** Every builder call is atomic because each registry method validates before it mutates. There is no batch registration.
- **Order.** Behavior order is global registration order. Notification handler order is global registration order per notification type. Request handler registration order has no observable effect.
- **Copies.** `Registrations.Copy()` produces an independent deep copy of all registrations. Copied-collection isolation already uses it, through `ArbitratorRegistry.CopyFor`.
- **Trimming annotations.** `AddHandler` and `AddOpenBehavior` carry `RequiresDynamicCode` and `RequiresUnreferencedCode`. `AddNotificationHandler` carries `RequiresDynamicCode` only.
- **Explicitness in the design.** `DESIGN.md` §2 lists assembly scanning as a non-goal. Principle 2, "Explicit over discovered", states that only types named in `AddArbitrator` take part in dispatch. §13 defers an "opt-in assembly scanning helper" that "would never become the default".

## Proposed solution

Add two scanning entry points to `ArbitratorBuilder`, each with a filter overload. Keep all type discovery in a new internal scanner, and all registration in the existing registry:

1. **Discovery.** A new internal `HandlerAssemblyScanner` reads every type of the assembly. It keeps the concrete classes that implement a request or notification handler contract, sorts them by full name, and applies the caller's filter. It returns the result as `DiscoveredHandlers`. It throws a configuration error when the types can't be loaded, or when nothing remains to register. Discovery runs outside the registry lock, because the filter is caller code.
2. **Registration.** A new registry method registers all discovered handlers as one unit. Under the lock, it copies the current `Registrations` and registers every discovered type into the copy, using the same logic as the single-type methods. It replaces the registry's registrations with the copy only when every type succeeds. A failure discards the copy, so nothing changes.
3. **Descriptors.** The builder adds the combined descriptors to the service collection, exactly as for explicit registrations.

To share the per-type logic between the single and batch paths, the bodies of `ArbitratorRegistry.RegisterHandler` and `RegisterNotificationHandler` move into `Registrations`, as instance methods that return the descriptors. The registry's single-type methods keep their signatures and call the moved logic on the live registrations. They are already atomic, because each validates before it mutates. The batch method calls the same logic on the copy.

This fits the codebase:

- It reuses the existing inspectors, error messages, duplicate rules, descriptor factory and deep copy.
- It adds no second validation path.
- It keeps the builder a thin façade.

## Codebase impact

- **Library (`src/Arbitrator`).**
  - Public: four new members on `ArbitratorBuilder`. No new public types.
  - Internal: a new scanner, a result type for discovered handlers, and a new error factory. One new batch method on `ArbitratorRegistry`, and two per-type registration methods moved into `Registrations`. The `_registrations` field of `ArbitratorRegistry` becomes reassignable. A new trimming message constant goes in `RegistrationReflection`.
  - Project file: the version becomes `0.3.0` with suffix `preview.1`. Description and tags are unchanged.
- **New library or package:** none. Scanning is a few hundred lines that depend only on the library's own internals and `System.Reflection`. Decision 18 (a single package) stands, and a separate package would need the registry to be public.
- **Tests (`tests/Arbitrator.Tests`):** a new `Scanning/` suite with its own fixture handlers and an assembly test double.
- **Documentation:** `DESIGN.md`, `README.md`, and this document.
- **Configuration, infrastructure, CI, persistence:** no changes.
- **Data models, migrations:** none.

## Target structure

Only new and changed paths are listed.

- `src/Arbitrator/`
  - `ArbitratorBuilder.cs`: changed. Adds the four scanning members, which delegate to the scanner and the registry.
  - `Arbitrator.csproj`: changed. The version becomes `0.3.0-preview.1`.
  - `Registration/`
    - `HandlerAssemblyScanner.cs`: new. An internal static class that discovers, sorts and filters handler types of one assembly.
    - `DiscoveredHandlers.cs`: new. An internal immutable record: the ordered request handler types and the ordered notification handler types to register.
    - `ScanningErrors.cs`: new. An internal static factory for the four scanning exceptions.
    - `ArbitratorRegistry.cs`: changed. Adds the batch method. The single-type handler methods delegate to `Registrations`.
    - `Registrations.cs`: changed. Gains the per-type registration logic for request handlers and notification handlers.
    - `RegistrationReflection.cs`: changed. Adds the scanning trimming message.
- `tests/Arbitrator.Tests/Scanning/`
  - `AssemblyScanningTests.cs`: new. Discovery, skipped types, filtering and ordering, against the test assembly and stub assemblies.
  - `ScanningRegistrationRulesTests.cs`: new. Atomicity, duplicates, freezing, read-only collections, null arguments, copied collections and filter re-entrancy.
  - `ScanningDiagnosticsTests.cs`: new. The four scanning errors, and validation errors raised for discovered types.
  - `AttributeLoadFailingType.cs`: new. A `TypeDelegator` whose attributes cannot be read, standing in for a type whose attribute comes from a missing assembly.
  - `StubAssembly.cs`: new. A test double that derives from `System.Reflection.Assembly`. It returns a chosen simple name and either a chosen set of types or a type-load failure. With it, tests control the input exactly, and need no extra projects or files on disk.
  - `Fixtures/`: new. Handler and notification types used only by scanning tests, under namespace `Arbitrator.Tests.Scanning.Fixtures`. It contains:
    - an internal request handler, and a private nested handler for a no-response request inside a static class
    - two notification handlers for one notification, `AlphaNoticeHandler` and `BetaNoticeHandler`, whose names sort in a known order and which share an abstract base
    - one type that is both a request handler and a notification handler
    - an open-generic handler definition, an interface extending a handler contract, and a struct implementing one
    - a closed behavior
    - plain request and notification records
    - `ScanFixtures`, the filter that selects this namespace
- `docs/design/assembly-scanning.md`: new (this document).
- `DESIGN.md`, `README.md`: changed, as listed under "Rollout and rollback".

Existing tests that register explicitly are unaffected. They never scan, and the fixtures live in their own namespace, which scanning tests select with the filter.

## Detailed behavior

### 1. Public API

All four members are on `ArbitratorBuilder`, return the builder for chaining, and are synchronous.

| Member | Contract |
| --- | --- |
| `AddHandlersFromAssembly(Assembly assembly)` | Scans `assembly` and registers every discovered handler |
| `AddHandlersFromAssembly(Assembly assembly, Func<Type, bool> filter)` | As above, but registers only the discovered types for which `filter` returns true |
| `AddHandlersFromAssemblyContaining<TMarker>()` | Scans the assembly that defines `TMarker` |
| `AddHandlersFromAssemblyContaining<TMarker>(Func<Type, bool> filter)` | As above, with a filter |

- **Marker type.** `TMarker` has no constraints. Any type identifies its defining assembly, including static classes, interfaces and structs.
- **Annotations.** Every member carries `RequiresUnreferencedCode`, with the new scanning message, and `RequiresDynamicCode`, with the existing dynamic-code message.
- **Scanning message.** The new message says that scanning discovers handler types at runtime, and that trimming can remove handler types that nothing else references.
- **Documentation.** Each member has XML documentation covering the discovery rules, the order, the filter, atomicity, and every exception below.

### 2. Discovered types

The scanner reads every type the assembly defines, including non-public and nested types. A type is a **candidate** when all of the following hold:

- It is a class: not an interface, struct, enum or delegate.
- It is not abstract. Static classes are abstract in metadata, so they are excluded too.
- It contains no unbound generic parameters. This excludes generic type definitions and types nested inside one.
- It is not marked with `CompilerGeneratedAttribute`.
- It implements at least one handler contract, directly or through a base class:
  - A **request handler contract** is any interface constructed from `IRequestHandler<TRequest, TResponse>` or `IRequestHandler<TRequest>`.
  - A **notification handler contract** is any interface constructed from `INotificationHandler<TNotification>`.

Types that fail any rule are skipped without an error. Visibility doesn't matter: `public`, `internal`, `private` and nested types are all candidates.

The `CompilerGeneratedAttribute` rule is checked last, and only for classes that implement a handler contract. Reading a type's attributes loads the assemblies that define them, so an attribute from an assembly that is missing at runtime must never affect classes that aren't handlers.

A candidate that implements both kinds of contract is registered as both a request handler and a notification handler, exactly as two explicit calls would register it. Implementing a behavior contract has no effect on scanning: a candidate that is also a behavior is registered as a handler only.

### 3. Order

- Candidates are sorted by `Type.FullName` with ordinal comparison. Nested types use the runtime's `+` separator, so `Outer+Inner` sorts by that string.
- The filter is invoked once per candidate, in sorted order.
- Kept types are registered in sorted order: first every request handler registration, then every notification handler registration.
- **Notification handlers** discovered by a scan therefore run after every handler registered before the scan call, ordered by full type name among themselves. Handlers registered by later calls run after them.
- **Request handlers.** Their order has no observable effect except which error is reported first. The first failing registration in that sequence determines the exception.
- **Exact control.** To control the order of particular notification handlers, the caller excludes them with the filter and registers them explicitly, before or after the scan.

### 4. Validation and errors

The steps run in this order. The first failure throws, and nothing is registered or added.

1. **Arguments.** A null `assembly` throws `ArgumentNullException` for parameter `assembly`, and a null `filter` throws it for parameter `filter`.
2. **Read-only collection.** A read-only service collection throws the existing read-only `ArbitratorConfigurationException`.
3. **Loading types.** If reading the assembly's types fails with `ReflectionTypeLoadException`, an `ArbitratorConfigurationException` is thrown. Its message:
   - names the assembly by its simple name
   - says the assembly can't be scanned because some of its types couldn't be loaded
   - lists at most five distinct, non-null loader messages, in their original order, followed by the number of further distinct messages when there are more
   - when there are no non-null loader messages, says that the inner exception has the details

   The `ReflectionTypeLoadException` is the inner exception. A scan never registers only the types that did load.

   If reading the attributes of a class that implements a handler contract fails with `FileNotFoundException`, `FileLoadException` or `TypeLoadException`, an `ArbitratorConfigurationException` is thrown. It names the assembly and the handler type, includes the original message, and carries the original exception as its inner exception.
4. **No candidates.** An assembly with no candidate types throws an `ArbitratorConfigurationException`. The message names the assembly, says it contains no request handler or notification handler types, and suggests checking that the marker type or assembly is the one that defines the handlers.
5. **Filter.** An exception thrown by the filter propagates unchanged, as the same instance, and nothing is registered.
6. **Filter excluded everything.** If the filter excludes every candidate, an `ArbitratorConfigurationException` is thrown. The message names the assembly and the number of candidates the filter excluded.
7. **Frozen registry.** If a service provider has already attempted to resolve `ISender` or `IPublisher`, the existing frozen-registry `ArbitratorConfigurationException` is thrown.
8. **Per-type validation.** Every kept type is validated exactly as `AddHandler` and `AddNotificationHandler` would validate it, against the registrations so far, including types registered earlier in the same scan. Any failure throws the existing exception with the existing message, unwrapped. Examples:
   - The type is already registered, whether explicitly or by an earlier scan.
   - The request type already has another handler.
   - A handler handles an abstract request type, or a request type with two request contracts.
   - A handler handles an abstract notification type.
   - The new pipeline would run a behavior twice.

Steps 3 to 6 run before the registry lock is taken. Steps 7 and 8 run under the lock.

### 5. Atomicity and concurrency

- Under the registry lock, the batch method:
  1. checks the freeze
  2. deep-copies the current registrations
  3. registers every kept type into the copy, collecting the descriptors
  4. replaces the registry's registrations with the copy
- Any exception in steps 1–3 leaves the registry's registrations untouched. The service collection is changed only after the registry commits, by adding the collected descriptors in the order they were produced.
- **Concurrent calls.** Concurrent builder calls on one service collection serialize on the existing lock, as they do today. A concurrent explicit registration either completes before the scan's copy is taken, or runs after the scan commits. It never lands in the discarded copy.
- **Filter re-entrancy.** The filter runs outside the lock. If it calls the builder itself, those registrations are committed before the scan's copy is taken, so the scan sees them and validates against them.

### 6. Unchanged behavior

- **Unchanged rules.** Lifetimes stay transient. Keyed registration, dispatch, the dispatch table, the provider check for registrations made after the provider was built, and container validation all work as today. A discovered handler is indistinguishable from an explicitly registered one.
- **Copied collections.** A scan on a copied collection first adopts its own registry, as any `AddArbitrator` call does. Scans on either collection don't affect the other.
- **Repeated scans.** Scanning the same assembly twice registers each candidate twice, so the second scan fails with the existing duplicate-handler-type error and changes nothing.

### 7. Edge cases

| Case | Outcome |
| --- | --- |
| A concrete handler and a concrete subclass of it | Both are candidates for the same request type. The second in sorted order fails with the "request type already has handler" error. The caller excludes one with the filter |
| An abstract handler base with concrete subclasses | The base is skipped. Each subclass is a candidate through its inherited contract |
| An open-generic handler definition | Skipped, as it can't be registered explicitly either |
| A `record` class handler | A candidate, because records are classes |
| A struct implementing a handler contract | Skipped, as the explicit methods require a class |
| The Arbitrator assembly itself (for example, marker `ISender`) | It defines no candidates, so the "no handler types" error is thrown |
| An assembly that has only notification handlers, or only request handlers | Valid. Whichever kind it contains is registered |
| A collectible or dynamic assembly | Not special-cased. Registered types keep a collectible assembly alive for the lifetime of the service collection and provider |

### 8. Diagnostics

New `DESIGN.md` §8.2 rows. The wording is representative, and tests assert on the exception type and on the assembly and type names a message contains.

| Failure | Stage | Representative message |
| --- | --- | --- |
| Types can't be loaded | Registration | `Assembly 'Contoso.Customers' cannot be scanned because some of its types could not be loaded: Could not load file or assembly 'Contoso.Shared, Version=1.0.0.0'. (and 2 more)` |
| Attributes of a handler type can't be loaded | Registration | `Assembly 'Contoso.Customers' cannot be scanned because the attributes of handler type 'Contoso.Customers.GetCustomerHandler' could not be loaded: Could not load file or assembly 'Contoso.Annotations, Version=1.0.0.0'.` |
| No handler types | Registration | `Assembly 'Contoso.Customers' contains no request handler or notification handler types. Check that the marker type or assembly passed to AddHandlersFromAssembly is the one that defines the handlers.` |
| Filter excluded every handler | Registration | `The filter excluded all 7 handler types found in assembly 'Contoso.Customers', so nothing was registered.` |

Arbitrator names assemblies by simple name only, or `(unnamed)` when an assembly has none. Loader messages are included verbatim, as the runtime produced them, and some runtime messages name files. The library logs nothing, and adds no metrics or traces.

### 9. Behavioral contract additions (`DESIGN.md` §7)

New rows 36 to 43:

- **36 Discovery.** A scan registers every candidate (§2) as a request handler, a notification handler, or both, and registers nothing else.
- **37 Order.** Discovered notification handlers run in ordinal full-name order, after handlers registered before the scan call.
- **38 Filter.** The filter is invoked once per candidate, in sorted order, and only accepted candidates are registered. Its exceptions propagate unchanged.
- **39 Strictness.** Discovered types obey every existing registration rule, including duplicates.
- **40 Atomicity.** A failing scan registers nothing and adds no descriptors.
- **41 Empty scan.** No candidates, or a filter that excludes every candidate, throws `ArbitratorConfigurationException`.
- **42 Unloadable types.** A type-load failure throws `ArbitratorConfigurationException`, with the loader exception as the inner exception.
- **43 Behaviors.** A scan never registers behaviors.

## Dependencies

No dependency is added, updated, replaced or removed. Discovery uses `System.Reflection` from the .NET 11 shared framework.

| Dependency | Change | Audit result |
| --- | --- | --- |
| None | n/a | `dotnet list package --vulnerable --include-transitive` against nuget.org on 2026-11-08 reported no vulnerable packages in `Arbitrator` or `Arbitrator.Tests` |

## Security considerations

- **Trust boundary.**
  - **The assembly.** The caller chooses which assembly to scan, in application code at startup. Arbitrator never loads an assembly by name or path, never enumerates loaded assemblies, and never reads assembly names from configuration, so there is no path for an attacker to choose the scanned code.
  - **The scanned types.** Discovery reads type metadata only. It instantiates nothing and runs no static constructors, and the DI container activates the registered types later, as it does for explicit registrations.
- **Unintended registration.** Scanning registers every handler in the assembly, including internal, test-only or debug handlers that a developer forgot. Several controls address this:
  - Scanning is opt-in and names exactly one assembly.
  - The filter can exclude types.
  - Strict duplicates stop two handlers silently competing for one request type.
  - The README states that every handler class in a scanned assembly becomes reachable through `ISender` or `IPublisher`.
- **Sensitive data in messages.** Error messages contain assembly simple names, type names and runtime loader messages, as existing messages already contain type names. They contain no request data, and Arbitrator adds no file paths of its own. Loader messages are included verbatim and can name an assembly or, for some runtime errors, a file path. They appear only at host startup, are capped at five, and the README already warns against returning Arbitrator messages to clients.
- **Denial of service.** Scanning is linear in the number of types in one assembly, and runs once at startup. Sorting is O(n log n). There is no recursion over assembly references.
- **Trimming.** In a trimmed application, handler types referenced only through scanning can be removed silently. The `RequiresUnreferencedCode` annotation makes the analyzer warn the caller, and no suppression is added.
- **No new attack surface.** There are no network calls, file access, deserialization or dynamic code loading.

## Testing strategy

All tests are TUnit tests in `tests/Arbitrator.Tests/Scanning/`. They run in parallel with the rest of the suite and share no mutable state.

- **Unit tests against `StubAssembly`.** These cover:
  - candidate rules
  - sort order, given types in reverse order
  - the type-load error, including the five-message cap, null loader entries and the inner exception
  - no candidates, and a filter that excludes everything
  - filter invocation order and count
  - a filter exception propagating as the same instance
- **Integration tests against the real test assembly**, selecting `Arbitrator.Tests.Scanning.Fixtures` with the filter. They build a provider and dispatch end to end, covering:
  - sending to discovered request handlers, including the no-response request
  - publishing to discovered notification handlers in name order
  - the dual-role type
  - the abstract-base subclass
  - skipped open-generic, behavior and plain types
- **Strictness and atomicity.**
  - **Duplicates.** Scanning after an explicit registration of a fixture fails with the existing duplicate error. Scanning twice fails the same way.
  - **Mid-scan failure.** A stub assembly whose third sorted type is invalid leaves the registry and the service collection unchanged. The descriptor count is unchanged, and the earlier types are still registrable explicitly afterwards.
  - **Freeze.** A scan after the freeze fails with the frozen error.
  - **Read-only.** A scan on a read-only collection fails with the read-only error.
  - **Behaviors still apply.** An open behavior registered before the scan wraps discovered request handlers.
  - **Filter re-entrancy.** A filter that registers a handler explicitly through the same builder doesn't deadlock. The scan validates against that registration, and when the filter registers a discovered type itself, the scan fails with the duplicate error.
- **Marker and `null` arguments.** `AddHandlersFromAssemblyContaining<TMarker>()` with a fixture marker scans the test assembly. With `ISender` as the marker, it throws the no-handler-types error for the Arbitrator assembly. Null arguments throw `ArgumentNullException` with the right parameter names.
- **Container validation.** A provider built with `ValidateOnBuild` and `ValidateScopes` over a scanned registration validates successfully.
- **Regression.** The full existing suite of 133 tests passes unchanged.
- **Analyzer.** The Release build has zero warnings, including trimming and AOT analyzers, with no new suppressions.
- **Performance tests:** not needed, because scanning runs once at startup over one assembly.

## Rollout and rollback

- **Release.** The work merges to `main` through a pull request that must pass "Build and test". It ships as `0.3.0-preview.1` through the existing release process (`docs/releasing.md`). The release notes state that scanning is opt-in, and that it is additive.
- **Documentation updates in the same change.**
  - `DESIGN.md`:
    - The status line mentions v0.3.
    - §1 gains a v0.3.0 paragraph.
    - §2 removes "Assembly scanning" from the non-goals and rewords principle 2 to: "Explicit over discovered: scanning is opt-in, and each call names its assembly; behaviors are never discovered."
    - §3 gains decision 23, summarizing the scanning choices.
    - §4.2 lists the four members and their annotations.
    - A new §5.13 summarizes the scanning contract and points here.
    - §5.11 records that the scanning members carry both annotations.
    - §7 gains rows 36–43.
    - §8.1 adds `assembly` and `filter` to the `ArgumentNullException` row.
    - §8.2 gains the three messages.
    - A new §10.5 points to these acceptance criteria.
    - §13 marks the scanning row as delivered in v0.3.
  - `README.md`: a new "Assembly scanning" section after "Notifications" covers:
    - usage with a marker type
    - the filter
    - behaviors staying explicit
    - the order rule
    - strict duplicates
    - the warning that every handler class in the assembly becomes reachable

    The "Trimming and Native AOT" section mentions the scanning members.
- **Migration:** none. Existing registrations keep working unchanged. Moving to scanning is optional, per application.
- **Feature flag:** none needed, because scanning only runs when called.
- **Rollback.** Consumers stop calling the scanning methods, or pin `0.2.0-preview.1`. Published versions can't be removed. A defective preview is unlisted and replaced by a higher version.

## Risks and mitigations

| Risk | Cause | Impact | Detection | Mitigation |
| --- | --- | --- | --- | --- |
| A forgotten or test-only handler becomes reachable | Scanning registers every handler in the assembly | Unintended behavior is reachable through `ISender` or `IPublisher` | Code review. Strict duplicates catch a competing handler for the same request | Opt-in only. The filter, and the README warning |
| Notification order changes after a rename | Order follows full type names | Handlers that depend on each other run in a different order | Tests in the consuming application | Documented rule, and handlers that depend on order are registered explicitly |
| A partial registration after a failure | A batch failure halfway through | Registry and container disagree, and later errors become confusing | Atomicity tests (row 40) | Copy, register, then commit under the lock. Descriptors are added only after the commit |
| Handlers silently missing | Trimming removes unreferenced handler types, or type-load failures are tolerated | Failures only at dispatch | The analyzer warning; the type-load error | `RequiresUnreferencedCode` annotation. Type-load failures are fatal |
| Concrete subclass of a concrete handler | A common inheritance pattern | Scan fails with a duplicate-handler error | Error message at startup | Documented edge case, fixed with the filter |
| Deadlock or inconsistent state from filter code | The filter calls the builder | Lock re-entrancy problems | Re-entrancy test | The filter runs outside the lock, and the copy is taken afterwards |
| `ArbitratorBuilder` grows beyond the 250-line type budget | Four new members with full XML documentation | A maintainability standard is breached | Measuring the type during implementation | Members are thin delegations, and the discovery logic lives in `HandlerAssemblyScanner` |
| `Registrations` grows too large after absorbing the per-type logic | Moving the registry method bodies | A maintainability standard is breached | Measuring the type during implementation | The moved logic is small. If the 250-line budget is reached, request-handler registration moves into `RegisteredPipelines` |
| The scanning design weakens the "explicit" opinion | Convenience features tend to grow | More options and more surprises | Design review | Behaviors are never discovered, there is no default scanning, and one assembly per call. Principle 2 is reworded rather than dropped |

## Pros and cons

**Pros**

- One line registers an assembly's handlers. New handlers are picked up without editing the registration list.
- No new validation path: discovered types meet exactly the existing rules and messages.
- The result is deterministic and atomic, and failures are loud and name the assembly.
- Pipeline order stays fully explicit.
- No new dependency, public type or package.

**Cons**

- Scanned notification handler order is tied to type names.
- Reflection over all types in an assembly, with trimming and AOT warnings on the new members.
- Strict duplicates make mixing scanned and explicit registration of the same type require a filter.
- A type-load problem anywhere in the assembly blocks the whole scan, even when the handlers themselves are fine.

**Alternatives considered**

- **Also discovering behaviors.** Rejected, because pipeline order would follow class names.
- **Separate scanning methods for request handlers and notification handlers.** Rejected: twice the surface, and the common case needs two calls.
- **A fluent scan builder.** Rejected: a second configuration style and more public types, for flexibility nobody has asked for.
- **Skipping already-registered types.** Rejected. It hides accidental double scans, gives explicit and scanned registrations different duplicate rules, and can't be made strict later without breaking users.
- **Metadata order instead of name order.** Rejected, because it isn't specified and can change with the compiler.
- **Public types only.** Rejected, because handlers are typically `internal sealed`.
- **Silently scanning only the loadable types.** Rejected, because missing handlers would surface only at dispatch.
- **A source generator.** Deferred. It would remove the reflection, but it is a much larger feature and remains in the §13 roadmap for Native AOT.

## Acceptance criteria

Each criterion has at least one test in `tests/Arbitrator.Tests/Scanning/`.

- **SC1** `AddHandlersFromAssembly` registers every candidate request handler. Sending each request reaches its discovered handler, including a no-response request.
- **SC2** Every candidate notification handler is registered. Publishing reaches them in ordinal full-name order, after a handler registered explicitly before the scan, and before one registered explicitly after it.
- **SC3** A type implementing both a request and a notification handler contract is reachable through both `ISender` and `IPublisher`.
- **SC4** Abstract classes, interfaces, structs, static classes, generic type definitions, compiler-generated types, behaviors and plain classes are not registered. A concrete subclass of an abstract handler base is registered.
- **SC5** `internal`, `private` nested and `public` handler types are all discovered.
- **SC6** `AddHandlersFromAssemblyContaining<TMarker>()` scans the assembly that defines `TMarker`.
- **SC7** The filter is invoked exactly once per candidate, in sorted order. Only accepted candidates are registered, and a filter exception propagates as the same instance with nothing registered.
- **SC8** An assembly with no candidates throws `ArbitratorConfigurationException` naming the assembly. With marker `ISender` it does the same for the Arbitrator assembly.
- **SC9** A filter that excludes every candidate throws `ArbitratorConfigurationException` naming the assembly and the excluded count.
- **SC10** A type-load failure throws `ArbitratorConfigurationException`. The message names the assembly and lists at most five distinct loader messages plus a count of the rest. The `ReflectionTypeLoadException` is the inner exception.
- **SC11** A scan that fails on any discovered type leaves the registry and the service collection unchanged. The same types can then be registered explicitly.
- **SC12** Scanning a type that is already registered, or scanning the same assembly twice, throws the existing duplicate-handler-type error.
- **SC13** A scan after the freeze throws the frozen-registry error. A scan on a read-only collection throws the read-only error.
- **SC14** Null `assembly` or `filter` throws `ArgumentNullException` with the matching parameter name.
- **SC15** An open behavior registered before the scan wraps discovered request handlers. No behavior is ever discovered.
- **SC16** A provider built with `ValidateOnBuild` and `ValidateScopes` over a scanned registration builds successfully, and container validation covers every discovered handler.
- **SC17** A scan on a collection copied from another affects only that collection.
- **SC18** The Release build has zero warnings with no new suppressions. All four members carry `RequiresUnreferencedCode` and `RequiresDynamicCode`, and the full existing test suite passes unchanged.
- **SC19** A filter that calls the builder doesn't deadlock, and the scan validates against what the filter registered.
- **SC20** `DESIGN.md` and `README.md` contain the updates listed under "Rollout and rollback". The package version is `0.3.0-preview.1`.
- **SC21** An attribute that can't be loaded on a class that isn't a handler doesn't affect the scan. On a handler class, it throws `ArbitratorConfigurationException` naming the assembly and the type, with the original exception as the inner exception. A type-load failure with no loader messages points to the inner exception.
