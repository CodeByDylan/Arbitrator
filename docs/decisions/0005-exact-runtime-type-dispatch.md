# 5. Dispatch by exact runtime type

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/requests-and-pipeline.md)

## Context

A request may be held through an interface or base-type reference. Falling back to a base type's handler makes the
handler that runs depend on the type hierarchy, which is hard to see in review.

## Decision

Arbitrator looks up `request.GetType()` in an immutable table and requires an exact match. It never falls back to a
base type.

## Consequences

- References typed as an interface or base type still reach the concrete handler.
- A derived request type needs its own handler. Sending one without a handler fails, and the message names the
  registered base type.
- Polymorphic dispatch isn't planned.
