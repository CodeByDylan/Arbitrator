# 9. Constrained open behaviors

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](../design/requests-and-pipeline.md)

## Context

Applications want behaviors that target some requests only, such as those implementing a marker interface, without a
separate filtering framework.

## Decision

Open behaviors may have generic constraints. A constrained behavior applies to a request exactly when the runtime
accepts its constraints for that request and response type.

## Consequences

- Behaviors can target requests through marker interfaces.
- Compile-time-only constraints, `notnull` and nullable annotations, aren't evaluated. If that causes confusion in
  practice, constrained open behaviors could be excluded in favor of checking marker interfaces inside the behavior.
