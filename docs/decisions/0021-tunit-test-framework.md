# 21. TUnit for tests

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](../design/requests-and-pipeline.md)

## Context

The tests need a framework on the current .NET test platform. They use the real MS.DI container and hand-written test
doubles.

## Decision

Tests use TUnit on Microsoft.Testing.Platform. No assertion or mocking packages are added.

## Consequences

- Discovery is source-generated, tests run in parallel by default, and assertions are asynchronous.
- Assertions are awaited, unlike xUnit's synchronous `Assert`.
