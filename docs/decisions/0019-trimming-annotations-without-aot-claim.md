# 19. Trimming annotations, no AOT claim

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/requests-and-pipeline.md)

## Context

Registration inspects interfaces through reflection and closes generic types at runtime, some over value types.
Explicit registration names the types, but doesn't remove that runtime generic instantiation.

## Decision

The library enables `IsAotCompatible`, so the trimming and AOT analyzers run. Each warning is resolved by an
annotation such as `[DynamicallyAccessedMembers]`, or by `RequiresDynamicCode` or `RequiresUnreferencedCode` on the
public entry point. Blanket suppressions aren't used. Native AOT isn't claimed.

## Consequences

- The library is honest about the reflection that remains.
- Native AOT consumers get warnings. A consumer that must publish with Native AOT would justify a source generator
  that emits closed registrations and invokers.
