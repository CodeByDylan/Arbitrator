# 20. Apache-2.0 license

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/requests-and-pipeline.md)

## Context

The package needs a license compatible with every dependency, shipped or used for testing. All of them are MIT or
Apache-2.0.

## Decision

Arbitrator is licensed under Apache-2.0. `LICENSE` holds the unmodified license text, and the package declares the
`Apache-2.0` license expression.

## Consequences

- The license includes an explicit patent grant.
- It is longer than MIT.
- No NOTICE file is needed while the project carries no third-party notices.
