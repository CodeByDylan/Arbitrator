# 14. One cancellation check, at entry

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](../design/requests-and-pipeline.md)

## Context

Work for a request whose token is already canceled is wasted. Checking between components would make Arbitrator
interpret cancellation, which belongs to the code doing the work.

## Decision

Arbitrator checks the token once at entry, after the handler lookup. After that it only forwards the token.

## Consequences

- Requests that are already doomed are skipped, and configuration errors still surface regardless of the token.
- Behaviors never see a send whose token was already canceled.
