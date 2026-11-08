# 36. Notification behaviors share the builder methods and the behavior order

- **Status:** Accepted
- **Date:** 2026-11-08
- **Design:** [notification pipeline](../design/notification-pipeline.md)

## Context

Notification behaviors need registration methods and an order. Decision 28 already lets `AddBehavior` and
`AddOpenBehavior` recognize request and stream behaviors from their interfaces.

## Decision

`AddBehavior` and `AddOpenBehavior` also recognize `INotificationBehavior<TNotification>`. An open notification
behavior is a generic type definition with exactly one type parameter. Notification behaviors take positions in the
one global behavior order (decision 8). They're registered under their own private service key.

## Alternatives considered

- **`AddNotificationBehavior` and `AddOpenNotificationBehavior`.** Rejected: more methods for one concept, against
  decision 28.
- **A separate order for notification behaviors.** Rejected: a second ordering rule.

## Consequences

- The open-behavior shape check accepts one-parameter notification definitions and two-parameter request or stream
  definitions, and rejects a definition that implements more than one kind.
- A closed behavior may implement request, stream, and notification contracts at once.
- `AddNotificationHandler` gains `RequiresUnreferencedCode`, because a new notification type must be wrapped by open
  notification behaviors registered earlier, which closes them at runtime.
