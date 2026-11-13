# 24. Documentation layout

- **Status:** Accepted, partly superseded by [41](0041-only-choices-worth-protecting-get-a-record.md) and [42](0042-feature-plans-live-in-the-pull-request.md)
- **Date:** 2026-11-08

## Context

`DESIGN.md` began as the v0.1 design and grew with every feature. It mixed the current contract, the reasons behind
it, internals, and the original implementation plan, so one feature had to edit a dozen sections, and the historical
parts went stale.

## Decision

Documentation is split by how it ages:

- **`docs/contract.md`** states what Arbitrator promises now, and changes with the behavior.
- **`docs/architecture.md`** explains the internals, and changes with them.
- **`docs/decisions/`** holds one record per decision. A record isn't edited after it's accepted, apart from its
  status when a later record supersedes it.
- **`docs/design/`** holds the design of each feature, frozen once the feature is implemented.
- **`src/Arbitrator/PublicAPI.*.txt`** records the public API ([25](0025-track-the-public-api.md)).

The v0.1 design is kept as `docs/design/requests-and-pipeline.md`, and `DESIGN.md` is removed.

## Consequences

- A feature adds a design and, for each real choice, a decision record. It edits only the parts of the contract and
  the README whose behavior changed.
- Feature designs written before this change refer to sections of `DESIGN.md`. They keep those references, with a note
  pointing to `requests-and-pipeline.md`, whose section numbers match.
- Deferred features are tracked as GitHub issues instead of a roadmap table, and the contract links to them.
- `AGENTS.md` points coding agents to `docs/README.md`, so the layout is followed without having to be discovered.
