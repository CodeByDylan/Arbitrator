# 3. No-response requests bridge to `Unit`

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](../design/requests-and-pipeline.md)

## Context

Requests without a response need the same pipeline as requests with one. Callers and handlers of such requests
shouldn't have to produce or receive a placeholder value.

## Decision

`IRequest` inherits `IRequest<Unit>`. `IRequestHandler<TRequest>` returns `Task` and bridges to
`IRequestHandler<TRequest, Unit>` with a default interface implementation. `SendAsync(IRequest)` returns `Task`.

## Alternatives considered

- **A separate pipeline for no-response requests.** Rejected: it doubles the behavior surface, and open behaviors
  would have to be written twice.
- **Handlers that return `Unit`.** Rejected: handlers would have to write `return Unit.Value`, the meaningless value
  this decision exists to avoid.

## Consequences

- There is one dispatch path and one pipeline. Callers and handlers never see `Unit`.
- `Unit` is public, and behaviors see it as `TResponse`.
- The name collides with `System.Reactive.Unit`, `MediatR.Unit`, and others when both namespaces are imported. Callers
  rarely name it. Collisions reported in real use would justify a rename, such as `NoResponse`, before 1.0.
