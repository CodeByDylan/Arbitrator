# 10. Scoped sender, transient components

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](../design/requests-and-pipeline.md)

## Context

Dispatch must use the caller's scope, and handlers must not capture dependencies that outlive them.

## Decision

`ISender` is scoped. Handlers and behaviors are transient, and their lifetime can't be changed.

## Alternatives considered

- **A singleton sender that finds the scope through `AsyncLocal` or `IHttpContextAccessor`.** Rejected: it's
  implicit, ties the library to ASP.NET Core, and breaks in background work.

## Consequences

- There are no captive dependencies and no state shared between sends. Scope validation catches a sender captured in a
  singleton.
- There is no singleton-handler optimization. Lifetime overloads can be added later without breaking anyone, if
  profiling shows construction cost matters or handlers need scoped state.
