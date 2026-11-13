# 11. Keyed internal registrations

- **Status:** Accepted
- **Date:** 2026-11-07
- **Design:** [requests and pipeline](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/requests-and-pipeline.md)

## Context

Arbitrator must resolve exactly the handler and behaviors it registered, and container validation should cover every
pipeline component.

## Decision

Handlers and behaviors are registered as their own implementation types under a private Arbitrator service key. Open
behaviors are registered as closed instantiations for each registered request.

## Alternatives considered

- **Resolving handlers by their `IRequestHandler<,>` service type.** Rejected: MS.DI silently returns the last
  registration, and proving there's only one would cost an enumeration on every dispatch.
- **Registering open behaviors as open generics in DI.** Rejected: container validation skips open-generic
  descriptors, as confirmed against MS.DI, so their dependency errors would appear only at dispatch.

## Consequences

- Arbitrator's registrations are isolated from the application's, so the container can never silently pick the last of
  several.
- Container validation covers every pipeline component.
- A provider must support keyed services. MS.DI does. Supporting a container whose adapter lacks
  `IKeyedServiceProvider` would need self-registration with uniqueness enforced at registration.
- The number of descriptors grows with handlers × open behaviors. Each is small; measured slow startup at thousands of
  handlers would reopen this.
