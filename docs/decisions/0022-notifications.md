# 22. Notifications

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [notifications](../design/notifications.md)
- **Supersedes:** [17](0017-defer-notifications.md)

## Context

Applications need in-process fan-out of events to any number of handlers, with the same predictability as requests.

## Decision

Notifications are published through a separate scoped `IPublisher` and registered with `AddNotificationHandler<T>()`.
Dispatch is by exact runtime type, to closed handlers only, sequentially in registration order. Cancellation is checked
before resolution and before each handler. There is no notification pipeline.

## Consequences

- The "exactly one" registrations of requests and the "zero or more" registrations of notifications stay apart.
- There are no handlers for base types and no open-generic handlers.
- Latency grows with the number of handlers.
