# 1. Async method naming

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/requests-and-pipeline.md)

## Context

The conceptual operations are "send" and "handle", and MediatR users expect `Send` and `Handle`. The owner's C#
standard requires asynchronous methods to end in `Async`. Renaming public methods later breaks every caller.

## Decision

Asynchronous methods are named `SendAsync`, `PublishAsync`, and `HandleAsync`.

## Consequences

- Names are longer than the conceptual ones and differ from MediatR habits.
- The names were settled before the contracts were written, so there was no churn.
