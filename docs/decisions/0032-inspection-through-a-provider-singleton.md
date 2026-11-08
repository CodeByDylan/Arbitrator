# 32. Registrations are inspected through a provider singleton

- **Status:** Accepted
- **Date:** 2026-11-08
- **Design:** [registry inspection](../design/registry-inspection.md)

## Context

Arbitrator can't prove that every request type in an application has a handler, so applications want to test it
themselves. They need to read what is registered. Integration tests of a real host usually hold the built service
provider, not the service collection.

## Decision

`AddArbitrator` registers a public singleton, `ArbitratorRegistrations`, which describes what that provider
dispatches. Resolving it builds the dispatch table: the registrations freeze, and the checks of the first `ISender` or
`IPublisher` resolution run, with the same exceptions. It is a singleton, so it can be resolved from the root provider
without a scope.

## Alternatives considered

- **A snapshot from the `IServiceCollection`.** Rejected: unusable once a host is built, and stale if more
  `AddArbitrator` calls follow.
- **Both.** Rejected: two ways to do one thing.

## Consequences

- Tests inspect exactly the configuration that dispatch uses, and only once it has passed validation.
- Inspecting a provider freezes its registrations, as sending through it does.
