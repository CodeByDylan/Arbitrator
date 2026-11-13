# 35. Notification behaviors wrap each handler and know its type

- **Status:** Accepted
- **Date:** 2026-11-08
- **Design:** [notification pipeline](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/notification-pipeline.md)
- **Supersedes:** the "no notification pipeline" part of [22](0022-notifications.md)

## Context

Decision 22 left notifications without a pipeline, so logging, timing, failure isolation, or a transaction around
each handler had to be repeated inside every handler. Concerns around the whole publish can already be handled by the
caller around `PublishAsync`.

## Decision

`INotificationBehavior<TNotification>` wraps each handler call: with three handlers, the behavior chain runs three
times, each time around one handler. Its `HandleAsync` receives the notification, the handler's implementation type,
a `NotificationContinuation`, and the token. The rest of decision 22 stands: exact-type dispatch, sequential
publishing in registration order, and the cancellation check before each handler, which now precedes that handler's
outermost behavior.

## Alternatives considered

- **One chain around the whole publish.** Rejected: callers can already wrap `PublishAsync`, and it can't isolate one
  handler's failure.
- **Both kinds of behavior.** Rejected: two abstractions for one concept.
- **Passing the handler instance.** Rejected: a behavior could call it directly and bypass `next`.
- **Passing nothing about the handler.** Rejected: per-handler logs and metrics couldn't tell handlers apart.

## Consequences

- Principle 1 becomes "one pipeline abstraction for each kind of message".
- A behavior that catches a handler's exception decides whether publication continues, as request behaviors do.
- A behavior meant to run once per publish needs another approach, such as wrapping `PublishAsync`.
