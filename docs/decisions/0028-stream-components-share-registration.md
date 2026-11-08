# 28. Stream components share the builder methods and the behavior order

- **Status:** Accepted
- **Date:** 2026-11-08
- **Design:** [request streaming](../design/request-streaming.md)

## Context

Stream handlers and stream behaviors need registration methods, and stream behaviors need an order.

## Decision

`AddHandler`, `AddBehavior`, and `AddOpenBehavior` work out from a type's interfaces whether it is a stream component,
just as `AddHandler` already finds every handler contract on a type. Assembly scanning registers stream handlers as
request handlers. Stream and other behaviors share one global registration order (decision 8). An open behavior must
implement exactly one of `IPipelineBehavior<,>` and `IStreamPipelineBehavior<,>`.

## Alternatives considered

- **Separate `AddStreamHandler`, `AddStreamBehavior`, and `AddOpenStreamBehavior` methods.** Rejected: six methods for
  one concept, and scanning would need its own rule for stream handlers.
- **A separate order for stream behaviors.** Rejected: a second ordering rule, while the two kinds never meet in one
  pipeline anyway.

## Consequences

- No new builder methods. Registration error messages name both kinds of contract.
- A class may be a handler or a closed behavior of both kinds at once, with one registration.
- Behavior positions in error messages count behaviors of both kinds.
