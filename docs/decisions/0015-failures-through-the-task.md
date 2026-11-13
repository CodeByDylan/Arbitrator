# 15. Failures arrive through the returned task

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/requests-and-pipeline.md)

## Context

A method that sometimes throws synchronously and sometimes faults its task behaves differently depending on whether,
and when, the caller awaits it.

## Decision

`SendAsync` reports every failure through its returned `Task` and never throws synchronously.

## Consequences

Every failure behaves the same way under `await`.
