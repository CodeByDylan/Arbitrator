#!/usr/bin/env bash
# Verifies that a GitHub Release may publish the package. Run from the root of a checkout that has the tagged commit
# checked out and origin/main fetched. Requires git, jq, and the .NET SDK.
# Inputs, passed as environment variables so release metadata never becomes part of the script text:
#   RELEASE_TAG    the tag of the release, for example v0.2.0-preview.1
#   IS_PRERELEASE  "true" when the release is marked as a pre-release, otherwise "false"
set -euo pipefail

readonly project_file="src/Arbitrator/Arbitrator.csproj"
readonly release_branch="origin/main"
readonly version_pattern='^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-(alpha|beta|preview|rc)\.[1-9][0-9]*)?$'

fail() {
    echo "::error::$1"
    exit 1
}

release_tag="${RELEASE_TAG:?RELEASE_TAG must be set}"
is_prerelease="${IS_PRERELEASE:?IS_PRERELEASE must be set}"

if [[ "$release_tag" != v* ]]; then
    fail "Release tag '$release_tag' must start with 'v', for example v1.2.0 or v1.2.0-preview.1."
fi

release_version="${release_tag#v}"

if [[ ! "$release_version" =~ $version_pattern ]]; then
    fail "Release tag '$release_tag' must be 'v' followed by MAJOR.MINOR.PATCH, optionally with a numbered alpha, beta, preview, or rc label, for example v1.2.0 or v1.2.0-preview.1."
fi

project_version="$(dotnet msbuild "$project_file" -getProperty:Version)"

if [[ "$release_version" != "$project_version" ]]; then
    fail "Release tag '$release_tag' does not match the project version '$project_version' in $project_file."
fi

if [[ "$release_version" == *-* ]]; then
    is_preview_version=true
else
    is_preview_version=false
fi

if [[ "$is_preview_version" == true && "$is_prerelease" != true ]]; then
    fail "Version '$release_version' is a preview, so the GitHub Release must be marked as a pre-release."
fi

if [[ "$is_preview_version" == false && "$is_prerelease" == true ]]; then
    fail "Version '$release_version' is stable, so the GitHub Release must not be marked as a pre-release."
fi

sdk_version="$(jq --raw-output '.sdk.version // empty' global.json)"

if [[ -z "$sdk_version" ]]; then
    fail "Could not read the SDK version from global.json."
fi

if [[ "$is_preview_version" == false && "$sdk_version" == *-* ]]; then
    fail "Stable version '$release_version' cannot be released while global.json pins the preview SDK '$sdk_version'. Release a preview version, or move global.json to a stable SDK first."
fi

if ! git merge-base --is-ancestor HEAD "$release_branch"; then
    fail "Release tag '$release_tag' points to a commit that is not on main. Releases must be tagged on a commit that has been merged to main."
fi

echo "Release $release_tag verified: it matches the project version, the release rules, and is on main."
