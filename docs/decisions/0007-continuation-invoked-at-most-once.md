# 7. A continuation runs at most once

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/requests-and-pipeline.md)

## Context

A behavior that calls `next` twice runs the handler twice, which duplicates side effects. A retry would also reuse the
same scoped state.

## Decision

`next` may be invoked at most once. A second call throws `InvalidOperationException`.

## Consequences

- Accidental duplicate side effects are caught.
- Retry behaviors can't live inside the pipeline.
- Allowing reuse later isn't a breaking change. Real retry needs inside the pipeline, together with accepting that a
  retry reuses the same scoped state, would justify it.
