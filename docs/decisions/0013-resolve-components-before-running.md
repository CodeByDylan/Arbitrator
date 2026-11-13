# 13. Resolve every component before running any

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/requests-and-pipeline.md)

## Context

If a component fails to activate after outer behaviors have started work, such as opening a transaction, the pipeline
is left partly executed.

## Decision

All behaviors are resolved first, outermost to innermost, then the handler, before any behavior runs.

## Alternatives considered

- **Resolving the handler lazily, when the innermost continuation runs.** Rejected for the reason above.

## Consequences

- An activation failure never leaves a pipeline partly executed.
- A short-circuiting behavior still pays for constructing the handler.
