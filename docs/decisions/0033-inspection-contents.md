# 33. Inspection describes handlers and behaviors as sorted, immutable data

- **Status:** Accepted
- **Date:** 2026-11-08
- **Design:** [registry inspection](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/registry-inspection.md)

## Context

The inspection API needs a shape that is easy to assert on, stable across runs, and cheap to extend.

## Decision

`ArbitratorRegistrations` is a sealed class with two read-only lists:

- `Requests`: one `RequestRegistration` per request type, holding the request type, the response type (the item type
  of a stream, `Unit` for `IRequest`), whether it is a stream, the handler type, and the closed behavior types that
  run for it, outermost first.
- `Notifications`: one `NotificationRegistration` per notification type, holding its handler types in run order.

Both lists are sorted by ordinal full type name. The entries are sealed classes with internal constructors, not
records, because record equality would compare the type lists by reference. There is no interface.

## Alternatives considered

- **Handlers only, without behaviors.** Rejected, so that tests can assert on pipeline composition too.
- **An `IArbitratorRegistrations` interface.** Rejected: there is one implementation, and adding members to a public
  interface breaks implementers.
- **One list with a kind for every entry.** Rejected: requests and notifications carry different data.

## Consequences

- Behavior order becomes observable, and therefore part of the contract it already was.
- Properties can be added later without breaking callers. Removing one would be breaking.
