# 17. Defer notifications

- **Status:** Superseded by [22](0022-notifications.md)
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/requests-and-pipeline.md)

## Context

The first version should keep its surface focused on requests.

## Decision

Notifications are deferred to v0.2.

## Consequences

Applications that need in-process fan-out must wait for v0.2 or write their own.
