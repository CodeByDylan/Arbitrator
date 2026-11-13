# 39. A scan registers every handler it finds with one lifetime

- **Status:** Accepted
- **Date:** 2026-11-12
- **Design:** [configurable lifetimes](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/configurable-lifetimes.md)

## Context

Assembly scanning registers many handlers at once. Once lifetimes are configurable, scans need a way to choose one.

## Decision

Each of the four scanning methods gains an overload taking a `ServiceLifetime`, which applies to every request,
stream, and notification handler that call registers. The existing overloads keep registering handlers as transient.
Handlers that need different lifetimes are split across scan calls with filters, or registered explicitly.

## Alternatives considered

- **Scans stay transient, and other lifetimes need explicit registration.** Rejected: assemblies of handlers that all
  need one lifetime would need a line per handler.
- **A per-type lifetime chosen by a callback.** Rejected: more API for a rarer case that filters already cover.

## Consequences

- Four more scanning overloads.
- One lifetime per scan call.
