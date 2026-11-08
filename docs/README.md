# Arbitrator documentation

| Document | What it holds | When it changes |
| --- | --- | --- |
| [`contract.md`](contract.md) | What Arbitrator promises: principles, semantics, the behavioral contract, diagnostics | In the same pull request as the behavior |
| [`architecture.md`](architecture.md) | How it's built, for contributors | When the internals it describes change |
| [`decisions/`](decisions/) | One record per decision, with its context and consequences | Never, once accepted. A new record supersedes an old one |
| [`design/`](design/) | The design of each feature | Never, once the feature is implemented |
| [`releasing.md`](releasing.md) | How to publish a version | When the release process changes |
| `src/Arbitrator/PublicAPI.*.txt` | The exact public API | With every public API change. The build enforces it |

The repository's `README.md` is the user guide.

## Adding a feature

1. Write the design in `docs/design/<feature>.md`. Earlier designs show the expected depth.
2. Record each real choice the feature makes as a new decision, numbered after the last one. If it reverses an earlier
   decision, set that record's status to "Superseded by N".
3. Implement it. Add new public API to `PublicAPI.Unshipped.txt`, update the contract sections and README sections
   whose behavior changed, add a row to the behavioral contract for each new rule with a test for it, and update
   `architecture.md` if the internals it describes changed.
4. Once implemented, set the design's status to "Implemented" and leave it unchanged after that. Close the enhancement
   issue the feature delivers, if there is one. Ideas deferred along the way become new enhancement issues.

## Decision record template

```markdown
# N. Title

- **Status:** Accepted
- **Date:** YYYY-MM-DD
- **Design:** link to the feature design, if there is one

## Context

The forces at play, and why a decision is needed.

## Decision

What was decided, in one or two paragraphs.

## Alternatives considered

The options rejected, and why. Omit the section when there were none.

## Consequences

What becomes easier or harder, and what would make the decision worth revisiting.
```
