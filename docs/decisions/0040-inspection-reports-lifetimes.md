# 40. Inspection reports lifetimes through component entries

- **Status:** Accepted
- **Date:** 2026-11-12
- **Design:** [configurable lifetimes](../design/configurable-lifetimes.md)

## Context

Once lifetimes are configurable, tests may want to assert them. `ArbitratorRegistrations` reports component types as
lists of `Type`, which can't carry a lifetime.

## Decision

A new public sealed class, `ComponentRegistration`, holds an implementation type and its `ServiceLifetime`.
`RequestRegistration` gains `Handler` and `Behaviors`, and `NotificationRegistration` gains `Handlers` and `Behaviors`,
all made of these entries in the same order as the existing type lists. The existing properties stay unchanged.

## Alternatives considered

- **Lifetime lists parallel to the existing type lists.** Rejected: callers would have to align two lists by index.
- **Replacing the existing properties.** Rejected: it would break callers of a released API.

## Consequences

- Each component appears both as a `Type` and inside an entry, which is redundant but keeps existing code working.
- Properties can be added to `ComponentRegistration` later without breaking callers.
