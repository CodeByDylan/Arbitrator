# 34. No built-in check for unhandled request types

- **Status:** Accepted
- **Date:** 2026-11-08
- **Design:** [registry inspection](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/registry-inspection.md)

## Context

The motivating use of inspection is a test asserting that every request type in an assembly has a handler.
Arbitrator could ship that check as a method.

## Decision

Arbitrator exposes the data only. The README shows the test, which compares an assembly's request types with
`ArbitratorRegistrations.Requests` in a few lines.

## Alternatives considered

- **A `FindRequestTypesWithoutHandler(Assembly)` method.** Rejected: it would fix which types count, such as abstract
  bases or test-only requests, and add a second reflection-based entry point with trimming annotations.

## Consequences

- Each application decides which request types must have handlers.
- The check is a few lines of test code that each application maintains.
