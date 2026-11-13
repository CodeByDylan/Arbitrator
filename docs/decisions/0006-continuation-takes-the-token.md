# 6. The pipeline continuation takes the token

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/requests-and-pipeline.md)

## Context

Behaviors such as timeouts need to give inner components a different cancellation token. A continuation that captures
the token hides where it comes from.

## Decision

Behaviors implement `HandleAsync(request, next, cancellationToken)`, where `next` is
`RequestContinuation<TResponse>(CancellationToken)`.

## Consequences

- Token forwarding is explicit, and a behavior can substitute a token.
- Behavior authors must pass a token to `next`.
- CA1716 flags any abstract or virtual method with a parameter named `next`. The rule is suppressed once on the
  interface, but consumers who write abstract behavior base classes see it too. Renaming the parameter to
  `continuation` before 1.0 would remove it everywhere.
