# 23. Opt-in assembly scanning

- **Status:** Accepted
- **Date:** 2026-11-08
- **Design:** [assembly scanning](../design/assembly-scanning.md)

## Context

Registering every handler by name is repetitive in applications with many handlers. Arbitrator's principle "explicit
over discovered" originally ruled scanning out.

## Decision

`AddHandlersFromAssembly(Assembly[, filter])` and `AddHandlersFromAssemblyContaining<TMarker>([filter])` register
request and notification handlers, never behaviors. Every concrete, closed class of any visibility is a candidate, in
ordinal full-name order. Duplicates are strict, and a scan registers all or nothing. An assembly without handlers, a
filter that excludes them all, and unloadable types are errors. The principle is narrowed: each scan names its
assembly, and scanning never becomes the default.

## Consequences

- One line per assembly, without giving up pipeline order, validation, or determinism.
- Notification handler order follows type names.
- Mixing scanned and explicit registrations of one type needs the filter.
