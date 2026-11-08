# 4. Response type inference

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](../design/requests-and-pipeline.md)

## Context

Callers should get a typed response without naming the response type at the call site.

## Decision

`ISender.SendAsync<TResponse>` takes an `IRequest<TResponse>`. `IRequest<TResponse>` is invariant, and a request type
implements exactly one request interface.

## Consequences

- C# infers `TResponse` from the request's single `IRequest<>` implementation.
- Registering a handler for a request type that implements two request interfaces is rejected.
