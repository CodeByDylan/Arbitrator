# 12. Duplicates are errors

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](../design/requests-and-pipeline.md)

## Context

When a mediator silently chooses between two registrations, the result depends on registration order that nobody
reviews.

## Decision

Duplicate handler contracts, duplicate handler types, duplicate behavior types, and behaviors that would run twice for
one request are all errors. A closed behavior that matches no registered handler is also an error, detected when the
first `ISender` is resolved rather than ignored as inert.

## Consequences

- Arbitrator never silently chooses.
- An accidental double module registration fails loudly.
- A closed behavior without a handler fails at first resolution, not at container build. The application-side
  container test in the contract catches it.
