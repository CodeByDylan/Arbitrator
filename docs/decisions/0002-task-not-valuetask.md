# 2. `Task`, not `ValueTask`

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](../design/requests-and-pipeline.md)

## Context

`ValueTask` avoids an allocation when an operation completes synchronously, but it can be awaited only once and can't
be safely stored or combined. Behaviors receive and await continuations, so those rules would fall on every behavior
author.

## Decision

Every asynchronous contract returns `Task` or `Task<TResponse>`.

## Consequences

- One `Task` is allocated when a handler completes synchronously. Handlers usually do I/O, and runtime async in
  .NET 11 makes `Task` cheaper.
- Per-dispatch allocations, the `Task` and the continuation objects, are accepted. Benchmark evidence that justifies
  pooling or specialization would reopen this.
