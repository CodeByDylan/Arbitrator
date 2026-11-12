# 38. Handlers and behaviors can be scoped or singleton

- **Status:** Accepted
- **Date:** 2026-11-12
- **Design:** [configurable lifetimes](../design/configurable-lifetimes.md)
- **Supersedes:** the "can't be changed" part of [10](0010-scoped-sender-transient-components.md)

## Context

Decision 10 made every handler and behavior transient. Some applications need state that lives for a scope, or want to
avoid constructing expensive components on every send.

## Decision

`AddHandler`, `AddNotificationHandler`, `AddBehavior`, and `AddOpenBehavior` each gain an overload taking MS.DI's
`ServiceLifetime`. All three lifetimes are allowed, and the existing overloads keep registering components as
transient. A class registered in two roles can have a different lifetime in each, because each role has its own
service key. An open behavior's lifetime applies to every closed instantiation of it. `ISender` and `IPublisher` stay
scoped.

## Alternatives considered

- **An attribute on the component class.** Rejected: discovered rather than stated where the component is
  registered, and it couples the class to Arbitrator.
- **One default lifetime per `AddArbitrator` call.** Rejected: too coarse.
- **Transient and scoped only.** Rejected: it doesn't serve the construction-cost case, which needs singleton.
- **Optional lifetime parameters on the existing methods.** Rejected: changing a public signature breaks compiled
  callers.

## Consequences

- Principle 8 no longer mentions transient-only lifetimes. Transient stays the default.
- "A new instance per send" and decision 37's "own instances per handler call" hold only for transient components.
- A singleton that depends on a scoped service is reported by container validation. Without validation it silently
  keeps one scoped instance for the application's lifetime, as any DI singleton would.
- Scoped and singleton components must tolerate concurrent use.
