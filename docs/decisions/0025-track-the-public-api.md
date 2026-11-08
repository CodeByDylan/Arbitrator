# 25. Track the public API

- **Status:** Accepted
- **Date:** 2026-11-08

## Context

The public API was listed by hand in `DESIGN.md`, which nothing checked against the code. A public change, such as an
added overload or a changed parameter type, could reach a release without anyone noticing it in review.

## Decision

The library references `Microsoft.CodeAnalysis.PublicApiAnalyzers` 5.6.0 as a build-only analyzer.
`PublicAPI.Shipped.txt` lists the API of the latest published version, and `PublicAPI.Unshipped.txt` lists changes
since. Because warnings are errors, the build fails when the code and the files disagree. A release moves the unshipped
entries into the shipped file.

The package is MIT-licensed, maintained in the Roslyn repository, and has no dependencies. It is a development
dependency only and isn't part of the published package. The NuGet vulnerability feed listed no advisories for it on
2026-11-08.

## Consequences

- Every public API change appears as a diff to the API files in review.
- Removing or changing a shipped member requires editing the shipped file, which makes breaking changes deliberate.
- Releasing has one more step.
