# Releasing Arbitrator

Arbitrator is published to [nuget.org](https://www.nuget.org/packages/Arbitrator) by the **Release** workflow
(`.github/workflows/release.yml`). Publishing a GitHub Release from a `v` tag starts it. The workflow uses NuGet
Trusted Publishing: it exchanges GitHub's short-lived identity token for a one-hour nuget.org API key. No NuGet API key
is stored anywhere.

The design behind this process is
[`ci-and-release.md`](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/ci-and-release.md),
as of the last commit that kept feature designs.

## Version rules

- **Single source.** The version comes from `VersionPrefix` and `VersionSuffix` in `src/Arbitrator/Arbitrator.csproj`.
  The release tag must equal `v` followed by that version, exactly.
- **Stable versions** are `MAJOR.MINOR.PATCH`, for example `0.2.0`.
- **Preview versions** add a numbered label: `alpha.N`, `beta.N`, `preview.N`, or `rc.N`, for example
  `0.2.0-preview.1`. Number previews from 1, so NuGet orders them correctly.
- **Pre-release flag.** A preview version must be published as a GitHub *pre-release*, and a stable version must
  not be.
- **Stable versions need a stable SDK.** A stable version can only be released when `global.json` pins a stable .NET
  SDK. While the SDK is a preview, only preview versions can be released.
- **Releases come from `main`.** The tag must point to a commit that has been merged to `main`. The workflow rejects
  tags on any other commit.
- **Versions are immutable.** A version that has been published can never be replaced. Releasing again requires a new
  version.

## Releasing a version

1. **Set the version.** Open a pull request that sets `VersionPrefix` and `VersionSuffix` in
   `src/Arbitrator/Arbitrator.csproj` to the new version. Move every entry from `src/Arbitrator/PublicAPI.Unshipped.txt`
   into `PublicAPI.Shipped.txt`, leaving only the `#nullable enable` line in the unshipped file. An entry marked
   `*REMOVED*` instead deletes its matching line from the shipped file. Update `README.md` if the release changes
   documented behavior. Merge it once the "Build and test" check passes.
2. **Draft the release.** On GitHub, under **Releases**, choose **Draft a new release**:
   - Create a new tag `v<version>` (for example `v0.2.0-preview.1`) targeting `main` at the merged commit.
   - Write the release notes, including any breaking changes.
   - Select **Set as a pre-release** if, and only if, the version has a label.
3. **Publish the release.** The Release workflow then runs four jobs:
   - **Verify:** checks the tag, the pre-release flag, and the SDK rule against the project version, and checks
     that the tagged commit is on `main`.
   - **Build:** runs the full CI build and test on the tagged commit.
   - **Publish to NuGet:** pushes the `.nupkg` and its `.snupkg` symbols to nuget.org.
   - **Attach release assets:** adds both files to the GitHub Release.
4. **Confirm.** Check that the version appears on nuget.org. Indexing can take a few minutes. Then install it in a
   scratch project, adding `--prerelease` for a preview.

Saving a draft, editing a published release, or pushing a tag on its own never publishes anything.

## One-time setup

These settings live outside the repository, and must be in place before the first release.

1. **nuget.org Trusted Publishing policy.** Signed in to nuget.org, open your account menu, choose
   **Trusted Publishing**, and add a policy owned by the publishing account:
   - Repository owner: `CodeByDylan`
   - Repository: `Arbitrator`
   - Workflow file: `release.yml`
   - Environment: `nuget`
   - Scope: allow publishing new packages and new versions, for the package `Arbitrator`.

   The policy is bound to the repository the first time it's used, so a re-created repository with the same name can't
   use it.
2. **GitHub environment.** In the repository settings, under **Environments**, create `nuget`. Under deployment
   branches and tags, choose **Selected branches and tags** and add the tag rule `v*`. Don't add required reviewers.
   Create it before the first release: if a workflow refers to an environment that doesn't exist yet, GitHub creates
   it automatically, without the tag rule.
3. **Repository variable.** Under **Secrets and variables → Actions → Variables**, create `NUGET_USER` with the
   nuget.org profile name of the publishing account. Use the profile name, not the email address. It's an identifier,
   not a secret.
4. **Actions permissions.** Under **Actions → General → Workflow permissions**, select
   **Read repository contents and packages permissions**. Leave **Allow GitHub Actions to create and approve pull
   requests** unchecked.
5. **Ruleset for `main`.** Under **Rules → Rulesets**, add a branch ruleset targeting the default branch with these
   rules:
   - require a pull request before merging
   - require the status check **Build and test**, with the branch up to date
   - block force pushes
   - restrict deletions

   Let repository administrators bypass the ruleset for emergencies.
6. **Ruleset for release tags.** Under **Rules → Rulesets**, add a tag ruleset targeting `v*` that restricts
   creations, updates, and deletions, with only repository administrators allowed to bypass it. Only administrators
   can then create release tags.

## When something goes wrong

| Situation | What to do |
| --- | --- |
| **Verify** fails | Read its error. It names the expected tag form, the project version, or the SDK rule. A tag on a commit that isn't on `main` is also rejected. Delete the GitHub Release and its tag, correct the version or the release settings, and publish again |
| **Build** fails | The tagged commit doesn't pass CI. Delete the release and tag, fix the problem on `main` through a pull request, and release again |
| **Publish to NuGet** fails at the login step | The Trusted Publishing policy, the `nuget` environment, or the `NUGET_USER` variable is missing or doesn't match. Fix the setting and re-run the failed jobs. Nothing was published |
| **Publish to NuGet** fails after pushing, or **Attach release assets** fails | Use **Re-run failed jobs**, not **Re-run all jobs**: re-running only the failed jobs reuses the packages already built in the run. An already-published version is skipped, and assets are replaced |
| A published version is broken | Unlist it on nuget.org and release a fixed, higher version. Published versions can't be deleted or overwritten |
| Trusted Publishing must be stopped immediately | Delete or deactivate the policy on nuget.org. No workflow can obtain a key without it |
