# Design: Continuous integration and NuGet releases

| | |
| --- | --- |
| Status | Implemented |
| Date | 2026-11-07 |
| Branch | `feature/ci-release`, from `main` at the v0.1 merge |
| Repository | `github.com/CodeByDylan/Arbitrator` (public) |

## Summary

Arbitrator has no automation. Builds, tests, formatting checks, and packaging run only on a developer machine, and nothing has been published to NuGet.

This design adds two GitHub Actions workflows:
- **CI** validates every pull request and every push to `main`: dependency audit, formatting, a Release build with warnings as errors, the full test suite, and packing.
- **Release** runs when a GitHub Release is published. It re-runs the CI validation, verifies that the release tag matches the project's version, and publishes the package to nuget.org using **Trusted Publishing**: a short-lived API key obtained through GitHub's identity token, so no long-lived NuGet secret ever exists. It also attaches the packages to the GitHub Release.

Dependabot keeps the pinned actions, the test packages, and the .NET SDK current.

## Goals

1. Every pull request to `main`, and every push to `main`, is built and fully tested on clean infrastructure, with results visible on the pull request.
2. Releases are deliberate, reviewed, and reproducible:
   - A release happens only when a maintainer publishes a GitHub Release from a `v`-prefixed tag.
   - The published version is exactly the version the project file declares.
3. Publishing to nuget.org uses Trusted Publishing only, with no stored NuGet API key. Only the release workflow, running in a protected environment for a release tag, can obtain a key.
4. Preview versions can be published while the pinned .NET SDK is itself a preview. A stable version can only be published once the pinned SDK is stable.
5. Workflows follow least privilege. Third-party code is pinned to exact commits and kept current automatically.

## Non-goals

- Testing on Windows or macOS. The library is pure managed code, and Linux is sufficient (decision 5A).
- Code coverage reporting, benchmarks, or performance gates.
- Automatic versioning from Git history, or automatic release-note generation.
- Publishing to feeds other than nuget.org, or nightly or continuous-preview packages.
- Package signing with a certificate. nuget.org repository-signs every package, and author signing isn't required.
- API compatibility validation against previously published versions. There is no published baseline yet.
- Automating GitHub repository settings or nuget.org configuration. Those are documented manual steps (decision 8A).

## Current state

- **Repository.**
  - **Settings:** public, default branch `main`, no `.github` directory, no GitHub environments, and no branch protection or rulesets.
  - **History:** v0.1 is merged (pull request 1). `feature/arbitrator-v0.2` is pushed but not yet merged.
- **Build.**
  - **Toolchain:** `global.json` pins SDK `11.0.100-rc.3.26522.148` with `latestFeature` roll-forward, allows prerelease SDKs, and selects Microsoft.Testing.Platform as the test runner.
  - **Build settings** (`Directory.Build.props`): warnings are errors, analysis level `latest-recommended`, code style enforced in the build, and NuGetAudit in mode `all`. `ContinuousIntegrationBuild` turns on automatically when `GITHUB_ACTIONS` is `true`.
  - **Package versions:** central package management in `Directory.Packages.props`. The only packages are test packages (`Microsoft.Extensions.DependencyInjection` 11.0.0-rc.3, `TUnit` 1.81.10).
- **Package.**
  - **Project:** `src/Arbitrator/Arbitrator.csproj` is the only packable project. It declares `VersionPrefix` `0.1.0` and `VersionSuffix` `preview`; the v0.2 branch raises the prefix to `0.2.0`.
  - **Metadata:** authors, copyright, project URL, the `Apache-2.0` license expression, the README, symbols as `snupkg`, and `PublishRepositoryUrl`.
  - **Dependencies:** none.
- **Tests.** TUnit on Microsoft.Testing.Platform, run with `dotnet test --project tests/Arbitrator.Tests`. TUnit brings the `Microsoft.Testing.Extensions.TrxReport` extension, so TRX result files can be produced without new packages.
- **NuGet.** The package ID `Arbitrator` is unclaimed on nuget.org.
- **Contract document.** `DESIGN.md` §9.5 currently states that nothing is published until the stable .NET 11 packages and SDK are adopted. This design changes that rule (decision 1A).

## Proposed solution

Three files under `.github/`, one documentation file, and small documentation edits.

1. **CI workflow** (`.github/workflows/ci.yml`, named "CI").
   - **Triggers:** pull requests targeting `main`, pushes to `main`, and calls from other workflows.
   - **Job:** one job, "Build and test", runs restore (with the vulnerability audit), the formatting check, a Release build, the tests, and packing. It uploads the test results and the packages as artifacts.
2. **Release workflow** (`.github/workflows/release.yml`, named "Release"). It runs when a GitHub Release is published, as four jobs:
   1. **Verify:** checks the tag against the project version and the release rules.
   2. **Build:** calls the CI workflow, so the package that's published is built and tested by exactly the same steps as every pull request.
   3. **Publish:** runs in the `nuget` environment, exchanges the GitHub identity token for a NuGet API key with `NuGet/login`, and pushes the package and symbols.
   4. **Attach assets:** uploads the package files to the GitHub Release.
3. **Dependabot** (`.github/dependabot.yml`) opens weekly update pull requests for GitHub Actions, NuGet packages, and the .NET SDK in `global.json`.
4. **Release procedure** (`docs/releasing.md`) documents how a maintainer prepares and publishes a release, and the one-time setup on GitHub and nuget.org.

**Why this fits.**
- **One definition of "validated".** CI is the single definition, reused by releases, so a release can't skip a check that pull requests run.
- **Version source.** The project file stays the single source of the version, as it is today, and the tag check makes any mismatch a hard failure.
- **No secrets.** Trusted Publishing removes the only secret the project would otherwise need.

## Codebase impact

- **No new library or project.** No change to any library or test source code.
- **New files:**
  - `.github/workflows/ci.yml`
  - `.github/workflows/release.yml`
  - `.github/scripts/verify-release.sh`
  - `.github/dependabot.yml`
  - `docs/releasing.md`
  - `docs/design/ci-and-release.md` (this document)
- **Changed files:**
  - **`DESIGN.md` §9.5:** the publishing rule becomes "preview versions may be published while the pinned SDK is a preview; stable versions require a stable pinned SDK, which the release workflow enforces".
  - **`DESIGN.md` §11, milestone M8:** the description now references the release workflow.
  - **`README.md`:** gains a CI status badge (the GitHub workflow badge for the CI workflow on `main`) and a short "Releases" note linking to `docs/releasing.md`.
- **No changes to `global.json`, the build props, the project files, or package versions.** Version bumps for actual releases happen in separate release pull requests, following `docs/releasing.md`.
- **No data, configuration values, or runtime behavior** are affected.

## Target structure

All paths are relative to the repository root.

- **`.github/workflows/ci.yml`:** the CI workflow ("CI"), with one job, "Build and test".
- **`.github/workflows/release.yml`:** the release workflow ("Release"), with the jobs Verify, Build (calling CI), Publish, and Attach assets.
- **`.github/scripts/verify-release.sh`:** the Verify job's rules as a standalone script, so they can be run locally against representative inputs (Testing strategy, step 4). Release metadata reaches it only through environment variables.
- **`.github/dependabot.yml`:** update configuration for three ecosystems: GitHub Actions, NuGet, and the .NET SDK.
- **`docs/releasing.md`:** the maintainer guide. It covers versioning rules, the step-by-step release procedure, the one-time setup checklist, and recovery from a failed or wrong release.
- **`docs/design/ci-and-release.md`:** this document.
- **`DESIGN.md`, `README.md`:** updated as described above.

Build outputs stay out of the repository: packages go to `artifacts/packages/`, and test results go to the `TestResults/` folder, which is already ignored. The implementation adds `artifacts/` to `.gitignore`.

## Detailed behavior

### 1. CI workflow ("CI")

**Triggers**
- Pull requests whose base branch is `main` (opened, synchronized, or reopened).
- Pushes to `main`.
- Calls from other workflows (`workflow_call`), with no inputs or secrets.
- `pull_request_target` is never used, so code from forks never runs with elevated permissions.

**Permissions.** The workflow-level token is reduced to reading repository contents. No other permission is granted, and no secrets are referenced.

**Concurrency.** Runs are grouped by the workflow name and the Git ref:
- A newer run for the same pull request cancels an older one that is still running.
- Pushes to `main` are never cancelled, so every commit on `main` has a result.
- When the Release workflow calls CI, the group name is derived from the Release workflow and the tag. It is distinct from the Release workflow's own concurrency group, so the two never block each other.

**Job "Build and test"** runs on the latest Ubuntu runner image, times out after 15 minutes, and performs these steps in order:
1. **Check out** the repository without persisting Git credentials in the working copy.
2. **Install the .NET SDK** named by `global.json`, using `actions/setup-dotnet` with its global-json option, so CI always uses exactly the repository's SDK.
3. **Restore** the solution. NuGetAudit runs in mode `all`, and because warnings are errors, any advisory against a direct or transitive package fails the job.
4. **Check formatting** for the solution, failing on any change it would make. This catches style and line-ending drift that `EnforceCodeStyleInBuild` doesn't cover.
5. **Build** the solution in the Release configuration, without restoring again. Warnings are errors, including the trimming and AOT analyzers.
6. **Test** the test project in Release, without rebuilding, producing a TRX report through the Microsoft.Testing.Platform TRX extension. A failing test fails the job.
7. **Pack** `src/Arbitrator` in Release, without rebuilding, into `artifacts/packages`. This produces the `.nupkg` and `.snupkg`. Because `ContinuousIntegrationBuild` is on, the package is deterministic and has SourceLink paths.
8. **Upload artifacts:**
   - **`test-results`:** the TRX files. Uploaded even when an earlier step failed, so failures can be inspected. Kept for 14 days.
   - **`packages`:** the `.nupkg` and `.snupkg`. Uploaded only when every step succeeded. Kept for 14 days.

**Outputs.** A pass or fail check named "Build and test" on the commit or pull request, and the two artifacts.

### 2. Release workflow ("Release")

**Trigger.** Publishing a GitHub Release, which covers both regular releases and pre-releases. Saving a draft doesn't trigger it, and nor do edits or deletions.

**Workflow-level permissions:** none. Each job declares only what it needs.

**Concurrency.** One group per release tag, with no cancellation. Runs for different versions proceed independently, because they publish different versions and don't interfere. Repeated runs for the same tag run one after the other. A single shared group is deliberately avoided: GitHub keeps only one pending run per group, so a third release published in quick succession would cancel the pending second one.

#### Job "Verify"
**Permissions:** read repository contents.

1. **Check out** the tagged commit.
2. **Install** the SDK from `global.json`.
The checks in steps 3 to 8 run in `.github/scripts/verify-release.sh`, in this order. The job checks out full history, so `main` is available for step 8.

3. **Check the tag format.** The tag must be `v` followed by a version that matches the allowed formats:
   - a stable version: three dot-separated non-negative integers with no leading zeros, or
   - a preview version: a stable version followed by a hyphen, a lowercase label from `alpha`, `beta`, `preview`, or `rc`, a dot, and a positive integer (for example `0.2.0-preview.1`).

   Any other tag fails the job with a message that states the expected form.
4. **Read the project version** from `src/Arbitrator` by asking MSBuild for the evaluated `Version` property. That combines `VersionPrefix` and `VersionSuffix` exactly as packing does.
5. **Compare.** The tag without its `v` prefix must equal the project version exactly. A mismatch fails with a message naming both values.
6. **Check the release kind.** The GitHub Release's pre-release flag must match the version: set for a preview version, cleared for a stable one.
7. **Gate stable releases on the SDK.** A stable version is allowed only when the SDK version in `global.json` (read precisely as `sdk.version` with `jq`, which hosted runners provide) has no prerelease label. A stable release attempted while the pinned SDK is a preview fails with a message explaining the rule.
8. **Require the commit to be on `main`.** The tagged commit must be reachable from `origin/main`. A tag on a commit that was never merged to `main` fails with a message stating the rule, so a release can't publish code that skipped review.

**Handling the release metadata.** The release's tag name and pre-release flag reach the script through environment variables, never by placing them directly into the script text, so a crafted tag name can't alter the commands that run.

#### Job "Build"
- **What it does:** calls the CI workflow through `workflow_call` on the tagged commit.
- **When:** only after "Verify" succeeds.
- **Permissions:** read repository contents.
- **Output:** the `packages` and `test-results` artifacts of this run.

#### Job "Publish"
- **When:** only after "Verify" and "Build" both succeed.
- **Environment:** `nuget`.
- **Permissions:** `id-token: write`, to request GitHub's identity token, plus read repository contents. Nothing else.
- **Timeout:** 10 minutes.

1. **Check out `global.json` only** (a sparse checkout, without persisted credentials), and install the SDK it names with `actions/setup-dotnet`, so every job in both workflows uses the same SDK from one source. No source code is checked out or built in this job.
2. **Download** the `packages` artifact produced by this run's "Build" job.
3. **Obtain an API key** with `NuGet/login`, passing the nuget.org profile name from the repository variable `NUGET_USER`. The action exchanges the identity token for a one-hour API key and exposes it as a step output.
4. **Push** the `.nupkg` to `https://api.nuget.org/v3/index.json` with that key, with the duplicate-skipping option enabled.
   - **Symbols:** `dotnet nuget push` also pushes the matching `.snupkg` automatically when it sits beside the package.
   - **Passing the key:** the key is given to the push command through an environment variable and is never printed. GitHub masks it in logs.
   - **Reruns:** skipping duplicates makes a rerun of a partly completed release safe: an already-published version is left untouched, and the job succeeds.

#### Job "Attach assets"
- **When:** only after "Publish" succeeds.
- **Permissions:** write repository contents, which is required to upload release assets.

Downloads the `packages` artifact and uploads the `.nupkg` and `.snupkg` to the GitHub Release that triggered the run, replacing same-named assets so reruns are safe.

**Outputs.** The version published on nuget.org (indexing can take some minutes after the push), the same files attached to the GitHub Release, and the run log.

### 3. Protecting the publish step

- **The `nuget` environment** allows deployments only from tags matching `v*`. A workflow running for a branch, or for any other tag, can't enter it, so it can never reach the publish step.
- **Scoping the nuget.org policy.** The Trusted Publishing policy names:
  - repository owner `CodeByDylan`
  - repository `Arbitrator`
  - workflow file `release.yml`
  - environment `nuget`

  nuget.org issues a key only for identity tokens from that workflow, in that environment, in that repository.
- **Binding to the repository.** The policy is bound to the GitHub repository and owner IDs on its first successful publish, so a deleted and recreated repository with the same name can't inherit it. On a public repository, nuget.org activates the policy immediately.
- **Policy scope.** Allows publishing new packages and new versions, limited to the package ID `Arbitrator`.

### 4. Version rules

- **Single source.** `VersionPrefix` and `VersionSuffix` in `src/Arbitrator/Arbitrator.csproj` define the version.
- **Release preparation.** A pull request sets them to the version being released, and that pull request goes through CI like any other.
- **Prerelease labels** are numbered (for example `preview.1`, `preview.2`), so NuGet orders previews correctly. The first release after this work is `0.2.0-preview.1`.
- **Immutability.** A version, once published, is never reused. Releasing again requires a new version.

### 5. Dependabot

Each ecosystem is checked weekly on Wednesday, the day after the usual .NET servicing release:

| Ecosystem | Directory | Grouping | Notes |
| --- | --- | --- | --- |
| GitHub Actions | repository root | All updates in one pull request | Updates the pinned commit SHAs and their version comments together |
| NuGet | repository root | Minor and patch updates in one pull request; major updates individually | Reads the central versions in `Directory.Packages.props` |
| .NET SDK | repository root | Its own pull request | Updates `global.json`. Prerelease SDKs are proposed only while `global.json` allows them |

- **Labels and limits.** Dependabot pull requests carry the label `dependencies` and are limited to five open per ecosystem.
- **Validation.** They run through CI like any other pull request.
- **No privileged trigger.** Dependabot-triggered runs receive a read-only token and no secrets, and the CI workflow needs neither.

### 6. Manual setup (one-time, by the owner)

These steps are settings, not files. `docs/releasing.md` lists them as a checklist, and none can be performed from the repository.

1. **nuget.org, while signed in:** add a Trusted Publishing policy owned by the personal account. Repository owner `CodeByDylan`, repository `Arbitrator`, workflow file `release.yml`, environment `nuget`, with a scope allowing new packages and new versions for `Arbitrator`.
2. **GitHub, environment:** create the environment `nuget`. Under deployment branches and tags, select "selected branches and tags" and add the tag rule `v*`. Add no required reviewers (decision 4A).
3. **GitHub, variable:** create the repository variable `NUGET_USER` holding the nuget.org profile name, not the email address. It's an identifier, not a secret.
4. **GitHub, Actions settings:** set the default workflow token permissions to read-only, and keep "Allow GitHub Actions to create and approve pull requests" disabled.
5. **GitHub, ruleset for `main`:**
   - Require a pull request before merging.
   - Require the status check "Build and test" to pass, with the branch up to date.
   - Block force pushes and deletion.

   Repository administrators may bypass the ruleset for emergencies.
6. **GitHub, ruleset for release tags:** a tag ruleset targeting `v*` that restricts creations, updates, and deletions, bypassable only by repository administrators.

Create the `nuget` environment (step 2) before the first release. If a workflow refers to an environment that doesn't exist, GitHub creates it automatically without the `v*` tag rule.

### 7. Release procedure (summarized; `docs/releasing.md` is authoritative)

1. Open a pull request that sets the version in `src/Arbitrator/Arbitrator.csproj` and updates `README.md` if needed. Merge it once CI passes.
2. On GitHub, draft a release:
   - Create the tag `v<version>` on the merged commit of `main`.
   - Write the release notes.
   - Mark it as a pre-release exactly when the version has a prerelease label.
3. Publish the release. The Release workflow verifies, builds, tests, publishes, and attaches the assets.
4. Confirm the version on nuget.org once indexing completes.

### 8. Failure and edge cases

| Situation | Behavior |
| --- | --- |
| A test, formatting, build, or audit failure in a pull request | The "Build and test" check fails. With the ruleset, the pull request can't merge |
| A pull request from a fork | CI runs with a read-only token and no secrets, so it behaves identically to any other pull request |
| A tag that doesn't match the project version | "Verify" fails. Nothing is built or published |
| Pre-release flag inconsistent with the version | "Verify" fails |
| A stable version while the pinned SDK is a preview | "Verify" fails with the SDK rule message |
| A test failure on the tagged commit | "Build" fails. Nothing is published |
| `NUGET_USER` missing, or the policy missing or mismatched | `NuGet/login` fails with nuget.org's error. Nothing is published, and the release can be re-run once fixed |
| A push that fails after the key was issued | The job fails. Re-running is safe because duplicates are skipped |
| The package is published but asset upload fails | Re-run the failed job. The upload replaces existing assets |
| A release published from a non-`v*` tag | The workflow starts, but "Verify" fails, and the environment would also refuse the publish job |
| A tag on a commit that isn't on `main` | "Verify" fails. Nothing is built or published |
| A release edited after publication | No workflow runs. Edits never publish |
| A Dependabot update that breaks the build | Its pull request fails CI and isn't merged |

## Dependencies

Every dependency is a GitHub Action used only in workflows, pinned to a full commit SHA with its version recorded beside it. Nothing ships in the package.

| Action | Version | Pinned commit | Purpose | License | Advisories |
| --- | --- | --- | --- | --- | --- |
| `actions/checkout` | v7.0.1 (latest) | `3d3c42e5aac5ba805825da76410c181273ba90b1` | Check out the repository | MIT | None |
| `actions/setup-dotnet` | v6.1.0 (latest) | `0f4a354dd5547a28c70b7853dbd95d5b67886e73` | Install the SDK named in `global.json` | MIT | None |
| `actions/upload-artifact` | v7.0.2 (latest) | `cf430e030ddbb5b0abf93d22962f4752f3646cd9` | Upload test results and packages | MIT | None |
| `actions/download-artifact` | v8.0.2 (latest) | `9000827ccba6bdab643e8b6fd33ac0654aef8333` | Pass packages to the publish and attach jobs | MIT | One historical advisory (affects 4.0.0–4.1.2 only); v8.0.2 is unaffected |
| `NuGet/login` | v1.2.0 (latest) | `8d196754b4036150537f80ac539e15c2f1028841` | Exchange the GitHub identity token for a short-lived nuget.org API key | Apache-2.0 | None |

**Why these and not alternatives.**
- **First-party.** All are first-party actions maintained by GitHub or by the NuGet team.
- **No replacements.** Attaching release assets uses the GitHub CLI preinstalled on hosted runners, and no third-party release action is added.
- **No new packages.** No NuGet packages or tools are added. TRX output comes from the `Microsoft.Testing.Extensions.TrxReport` extension that TUnit already brings.

**Audit method and date (2026-11-07).**
- **Versions and pins:** latest release versions and tag commits were read through the GitHub REST API.
- **Advisories:** checked in the GitHub Advisory Database (ecosystem `actions`) for each action.
- **Licenses:** read from each repository's metadata.

## Security considerations

- **No long-lived NuGet secret.** Publishing uses Trusted Publishing only. The key nuget.org issues is valid for one hour and is obtained immediately before the push.
- **Narrow key issuance.** Only the release workflow, in the `nuget` environment, for a `v*` tag of this repository, can obtain a key, because the nuget.org policy and the environment rule both enforce it.
- **Least privilege.**
  - **CI:** the workflow-level token reads repository contents only.
  - **Release:** grants nothing at the workflow level.
  - **Publish:** only the publish job may request an identity token, and only the attach job may write repository contents.
  - **Defaults:** the repository's default token permissions are read-only (manual step 4).
- **Untrusted code.**
  - **Fork pull requests:** run CI with no secrets and a read-only token.
  - **No elevated contexts:** `pull_request_target` and `workflow_run` are not used, so pull request code never runs with elevated permissions or secrets.
  - **Isolated publish:** the publish job checks out only `global.json` and builds nothing; it only pushes artifacts built and tested earlier in the same run.
- **Script injection.** Values that could come from user input (tag name, release metadata) are passed to scripts through environment variables, never interpolated into script text.
- **Supply chain.**
  - **Pins:** every action is pinned to a full commit SHA, and Dependabot proposes updates as reviewable pull requests.
  - **Package audit:** NuGetAudit fails CI on any advisory against direct or transitive packages.
  - **Credentials:** checkout doesn't persist credentials.
- **Logs.** The API key is never printed: GitHub masks it, and it's passed through an environment variable. Test results contain test names and assertion messages only. The library's tests use no personal or secret data.
- **Release integrity.** The published version must equal the reviewed project version, and the tagged commit must be on `main`, where the ruleset prevents unreviewed changes. The tag ruleset limits who can create release tags to administrators.

## Testing strategy

Workflows are infrastructure, so they're validated by running them:

1. **Static validation.**
   - **YAML syntax:** each workflow and the Dependabot file parse as valid YAML.
   - **Workflow linting:** the implementation lints the workflows with `actionlint` if it can be run without adding a dependency to the repository; otherwise the first CI run serves as the check.
2. **Pull request validation.** Opening the pull request for this branch runs CI. It must pass on the current code, and its artifacts must contain the TRX results and both package files.
3. **Negative checks, before merging, on a throwaway branch that is deleted afterwards:**
   - a deliberately failing test
   - a deliberate formatting violation

   Each must fail the "Build and test" check.
4. **Release verification logic.** The tag, version, pre-release, and SDK rules are exercised before the first real release by running the Verify job's script locally against representative inputs:
   - a matching preview tag
   - a mismatched tag
   - a stable tag with a preview SDK
   - a malformed tag
   - an inconsistent pre-release flag
5. **First real release.** After v0.2 and this work are merged, `0.2.0-preview.1` is the end-to-end test. It must be published on nuget.org with symbols, attached to the GitHub Release, and found by `dotnet add package Arbitrator --prerelease` in a scratch project outside the repository.

## Rollout and rollback

**Rollout**
1. Merge `feature/arbitrator-v0.2` (v0.2.0 code) and this branch, each through its own pull request.
2. The owner performs the one-time setup steps in Detailed behavior §6.
3. Open a release pull request setting `VersionSuffix` to `preview.1`.
4. Publish the GitHub Release `v0.2.0-preview.1`, marked as a pre-release.

**Rollback**
- **Workflows:** revert the commit that added them. No external state depends on them.
- **A bad package version:** unlist it on nuget.org. Published versions can't be deleted or replaced, so a fix ships as a new version. Delete or edit the GitHub Release if needed.
- **Trusted Publishing:** delete or deactivate the policy on nuget.org. Without it, no workflow can publish.

## Risks and mitigations

| Risk | Why it could occur | Impact | Detection | Mitigation |
| --- | --- | --- | --- | --- |
| Policy misconfigured | Typo in owner, repository, workflow file, or environment | The first release fails at login | `NuGet/login` error in the run | Checklist in `docs/releasing.md`. Re-run after fixing; nothing is published in the meantime |
| Stable release attempted before .NET 11 is stable | A maintainer prepares a stable version too early | A stable package built with a preview SDK | "Verify" job | The SDK rule rejects it |
| Preview SDK no longer downloadable | Microsoft removes or replaces RC builds after general availability | CI can't install the SDK | Setup step fails | `global.json` rolls forward to newer feature bands, and Dependabot's SDK updates move the pin to general availability |
| Dependabot noise | Frequent updates across three ecosystems | Review load | Open pull request count | Weekly schedule, grouping, and a limit of five pull requests |
| A compromised upstream action | Supply-chain attack on an action repository | Malicious code in workflows | Dependabot diffs and advisories | Full-SHA pins, least-privilege tokens, and only the publish job can obtain an identity token |
| Ruleset not configured | The manual step is skipped | Failing changes can reach `main` | Repository settings review | Listed in the setup checklist; CI still reports failures |
| A rerun after a partial publish | Transient failure after the push | Confusing state | Job status | Duplicate skipping and asset replacement make reruns safe |
| Runner image changes | The latest Ubuntu image moves to a new release | Unexpected CI differences | CI failure | The SDK comes from `global.json`, not the image; only basic tools come from the image |

## Pros and cons

**Pros**
- **Fail-safe releases.** They are deliberate, reviewed, versioned from one source, and can't publish an untested or mismatched package.
- **No secrets to store, rotate, or leak.**
- **Minimal surface.** Three small files and first-party actions only, with every privilege scoped to the job that needs it.
- **One definition of "validated",** shared by pull requests and releases.

**Cons**
- **More manual steps.** Creating a release takes a version pull request and a GitHub Release, more than tagging and pushing.
- **Some setup lives outside the repository** (nuget.org and GitHub settings) and must be done by the owner.
- **Linux only.** A platform-specific problem would go unnoticed, which is unlikely for a pure managed library.

**Alternatives considered**
- **Tag-push trigger.** Simpler, but it has no release notes or pre-release flag, and an accidental tag push publishes.
- **Manual dispatch.** It lacks a durable release record.
- **Versions from tags or Git history** (for example with a versioning tool). That adds a dependency, and the project file and published versions could drift.
- **A stored NuGet API key.** A long-lived secret with rotation burden and leak risk.
- **Required approval on the environment.** It adds a second manual step after an already deliberate release action.
- **A Windows and macOS matrix.** Triples CI time for no expected benefit, and needs line-ending normalization.
- **Third-party release or publish actions.** Unnecessary; the GitHub CLI and `dotnet nuget push` cover them.

## Acceptance criteria

**CI**
- **CI1.** A pull request to `main` triggers "CI". Its "Build and test" job runs restore with audit, the formatting check, a Release build, the tests with TRX output, and packing, and passes on the current code.
- **CI2.** The run uploads a `test-results` artifact with at least one TRX file, and a `packages` artifact with exactly one `.nupkg` and one `.snupkg` for `Arbitrator`.
- **CI3.** A failing test, or a formatting violation, fails "Build and test", as shown by negative runs on a throwaway branch.
- **CI4.** The CI workflow's token is read-only, it references no secrets, and it doesn't use `pull_request_target`.
- **CI5.** Pushing a second commit to an open pull request cancels the earlier run for that pull request.

**Release**
- **RE1.** Publishing a GitHub Release whose tag doesn't equal `v` plus the project version fails "Verify", and nothing is published.
- **RE2.** A pre-release flag that is inconsistent with the version fails "Verify".
- **RE3.** A stable version, while `global.json` names a preview SDK, fails "Verify" with a message stating the SDK rule.
- **RE4.** For a valid preview release, the workflow builds and tests through the CI workflow, then publishes the version to nuget.org with its symbols using Trusted Publishing. No secret is configured in the repository.
- **RE5.** The same `.nupkg` and `.snupkg` are attached to the GitHub Release.
- **RE6.** Re-running a completed or partly completed release run succeeds without changing the published package.
- **RE7.** Only the publish job can request an identity token, only the attach job can write contents, and the workflow grants no permissions at the top level.
- **RE8.** A release whose tag points to a commit that isn't on `main` fails "Verify", and nothing is published.
- **RE9.** Release runs for different tags never cancel each other.

**Supply chain and documentation**
- **SC1.** Every `uses` reference in the workflows is pinned to a full commit SHA, with the version noted beside it.
- **SC2.** Dependabot is configured for GitHub Actions, NuGet, and the .NET SDK on a weekly schedule, with the grouping and limits in Detailed behavior §5.
- **DO1.** `docs/releasing.md` contains the version rules, the release procedure, the one-time setup checklist, and recovery steps.
- **DO2.** `DESIGN.md` §9.5 states the new publishing rule, and the README shows the CI badge and links to `docs/releasing.md`.
- **DO3.** The first release, `0.2.0-preview.1`, is installable from nuget.org in a project outside the repository.
