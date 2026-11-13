# 42. A feature's plan lives in its pull request, not in the repository

- **Status:** Accepted
- **Date:** 2026-11-13

## Context

[24](0024-documentation-layout.md) kept each feature's design in `docs/design/`, frozen once the feature was
implemented. Everything lasting in a design moves into the contract, the README, the public API files,
`architecture.md`, and decision records while the feature is built, so the frozen design is a third copy that is never
updated. By 0.7.0 the designs held 3,693 lines, against 823 for the contract and the architecture together, and parts of
them were no longer true: `requests-and-pipeline.md` still says handlers and behaviors are always transient, which
[38](0038-configurable-component-lifetimes.md) changed. A search of the docs finds such statements with nothing to show
they are outdated.

## Decision

A feature starts with its spec. On the feature's branch, the contract sections, `PublicAPI.Unshipped.txt`, and the
README sections are changed to describe the feature, with a behavioral contract row for each new rule. These changes
are reviewed before the code. The plan (summary, approach, steps, risks, rejected alternatives, and acceptance criteria) is
the pull request's description. Nothing else about the feature is kept in the repository.

`docs/design/` is removed. The **Design** link of each record now points to its design at commit `f2bb0a6`, the last
commit that has them, so the links keep working.

## Alternatives considered

- **Keeping the designs, marked as historical.** Rejected: a banner doesn't stop a search from finding outdated
  statements.
- **Keeping the designs up to date.** Rejected: they would duplicate the contract, which already changes with the
  behavior.
- **Plans in GitHub issues.** Rejected: an issue states the need, while the plan belongs with the change that carries
  it out, and a pull request shows both.

## Consequences

- The documentation in the repository describes only the current state.
- The reasoning behind a feature is in its pull request and the git history. Its lasting parts are decision records.
- A spec reviewed as a diff of the contract is harder to follow than a narrative, so a large feature's pull request
  opens with a short overview.
- Repointing the **Design** links is a one-time exception to the rule in [24](0024-documentation-layout.md) that
  accepted records aren't edited.
