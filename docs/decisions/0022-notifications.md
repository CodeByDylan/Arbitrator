# 22. Notifications

- **Status:** Accepted, partly superseded by [35](0035-per-handler-notification-behaviors.md)
- **Date:** 2026-11-07
- **Design:** [notifications](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/notifications.md)
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
