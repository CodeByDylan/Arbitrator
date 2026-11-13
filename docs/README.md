# Arbitrator documentation

| Document | What it holds | When it changes |
| --- | --- | --- |
| [`contract.md`](contract.md) | What Arbitrator promises: principles, semantics, the behavioral contract, diagnostics | In the same pull request as the behavior |
| [`architecture.md`](architecture.md) | How it's built, for contributors | When the internals it describes change |
| [`decisions/`](decisions/) | One record per decision worth protecting, with its context and consequences, and a generated [index](decisions/README.md) | Never, once accepted. A new record supersedes an old one |
| [`releasing.md`](releasing.md) | How to publish a version | When the release process changes |
| `src/Arbitrator/PublicAPI.*.txt` | The exact public API | With every public API change. The build enforces it |

The repository's `README.md` is the user guide. A feature's plan is the description of its pull request and isn't kept
in the repository ([42](decisions/0042-feature-plans-live-in-the-pull-request.md)).

## Adding a feature

1. **Write the spec.** On the feature's branch, change the contract sections, `PublicAPI.Unshipped.txt`, and the README
   sections to describe the feature as it will behave, and add a row to the behavioral contract for each new rule.
   Small changes start here and need nothing else before the code.
2. **Write the plan** as the pull request's description: what the feature does and why, the approach, the steps, the
   risks, the alternatives rejected, and the acceptance criteria. A large feature opens with a short overview, since
   the spec alone is a diff.
3. **Record the decisions.** Check each real choice against [the bar for a record](#when-a-choice-gets-a-record). List
   the ones that pass in the plan, and record each that the maintainer approves as a new decision, numbered after the
   last one. The rest stay in the plan's alternatives. If a record reverses an earlier decision, set that record's
   status to "Superseded by N", or "Accepted, partly superseded by N" when only part of it changes. Then run
   `.github/scripts/build-decision-index.sh`.
4. **Implement it.** Add a test for each new row of the behavioral contract, keep the spec in step with what was built,
   and update `architecture.md` if the internals it describes changed.
5. Close the enhancement issue the feature delivers, if there is one. Ideas deferred along the way become new
   enhancement issues.

## When a choice gets a record

A choice gets a decision record only if it passes all three tests:

1. **Costly to reverse.** Changing it later breaks callers, a release, or a lot of code. Examples: public API shape,
   promised behavior, dependencies, license, packaging.
2. **Likely to be undone.** A capable contributor reading the code without the record might "fix" or "simplify" it. If
   the obvious option was chosen, there's nothing to protect.
3. **A real alternative lost.** A viable option was rejected for a reason specific to this case, not only because of a
   rule already written elsewhere.

Every other choice goes where it belongs:

| Kind of choice | Where it goes |
| --- | --- |
| A trade-off inside one feature | The plan in its pull request, under the alternatives |
| A naming or style convention | `.editorconfig` or an analyzer when it can be enforced, otherwise the plan that introduces it |
| What Arbitrator does | [`contract.md`](contract.md) |
| Something Arbitrator won't do | One line under "Not planned" in [`contract.md`](contract.md) §2 |
| Something the build or tests already enforce | Nowhere else. The check is the record |

The title of a record states the decision itself, such as "Duplicates are errors", so that the
[index](decisions/README.md) is enough to find it. Where code looks wrong but is deliberate because of a record, a
one-line comment may point to the record.

## Decision record template

```markdown
# N. Title

- **Status:** Accepted
- **Date:** YYYY-MM-DD
- **Feature:** the feature's name, linked to its pull request once it exists

## Context

The forces at play, and why a decision is needed.

## Decision

What was decided, in one or two paragraphs.

## Alternatives considered

The options rejected, and why. Omit the section when there were none.

## Consequences

What becomes easier or harder, and what would make the decision worth revisiting.
```
