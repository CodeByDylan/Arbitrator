# 21. TUnit for tests

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/requests-and-pipeline.md)

## Context

The tests need a framework on the current .NET test platform. They use the real MS.DI container and hand-written test
doubles.

## Decision

Tests use TUnit on Microsoft.Testing.Platform. No assertion or mocking packages are added.

## Consequences

- Discovery is source-generated, tests run in parallel by default, and assertions are asynchronous.
- Assertions are awaited, unlike xUnit's synchronous `Assert`.
