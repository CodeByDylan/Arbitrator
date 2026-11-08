# 18. A single package

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](../design/requests-and-pipeline.md)

## Context

Mediator libraries often ship contracts in a separate abstractions package, so that assemblies defining requests
don't depend on the dispatcher. Arbitrator has no package dependencies at all: the DI abstractions it uses ship in the
.NET 11 shared framework, and a package reference to them fails restore with NU1510. No second container integration
exists, and the whole library is small.

## Decision

Arbitrator ships one package, `Arbitrator`, containing the contracts, the dispatcher, and the MS.DI integration.

## Consequences

- Assemblies that only define contracts also reference `Microsoft.Extensions.DependencyInjection.Abstractions`,
  through the shared framework.
- The contracts can move later to a split-out package that the main package depends on, with `[TypeForwardedTo]`
  keeping existing binaries working.
