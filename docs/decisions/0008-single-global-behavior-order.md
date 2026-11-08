# 8. One global behavior order

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](../design/requests-and-pipeline.md)

## Context

Pipelines need a predictable order. Priorities and groups add rules that interact in ways that are hard to predict.

## Decision

One global registration order covers open and closed behaviors across all `AddArbitrator` calls. The first registered
behavior is outermost.

## Consequences

- There is a single ordering rule, with no priorities or groups.
- Order across modules depends on the order of the modules' `AddArbitrator` calls.
