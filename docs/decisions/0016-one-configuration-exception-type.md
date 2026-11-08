# 16. One configuration exception type

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](../design/requests-and-pipeline.md)

## Context

Hosts and tests need to tell an Arbitrator misconfiguration apart from an `InvalidOperationException` thrown by a
handler. Cancellation, handler failures, and activation failures are already described well by existing exceptions.

## Decision

Arbitrator has one custom exception, `ArbitratorConfigurationException`, deriving from `InvalidOperationException`.
Everything else uses standard BCL exceptions or passes through unchanged.

## Consequences

- One more public type.
- Callers catching `InvalidOperationException` still catch configuration errors.
