# 37. Each notification handler gets its own behavior instances

- **Status:** Accepted
- **Date:** 2026-11-08
- **Design:** [notification pipeline](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/notification-pipeline.md)

## Context

When a publish reaches several handlers, the behaviors around them can be one set shared by all handlers, or a
separate set for each.

## Decision

Each handler call gets its own instance of every applicable behavior, as each send gets its own instances. All
behaviors and handlers of a publish are resolved before the first one runs (decision 13): for each handler in run
order, its behaviors outermost first, then the handler.

## Alternatives considered

- **One set of behavior instances per publish.** Rejected: one instance would run several times in one publish,
  which breaks behaviors that keep state between before and after `next`.

## Consequences

- A behavior's instance state always belongs to one handler call.
- A publish with N handlers and M behaviors resolves N × M behavior instances.
