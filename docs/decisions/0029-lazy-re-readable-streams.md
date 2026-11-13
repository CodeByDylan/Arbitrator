# 29. Streams are lazy, and each read is a dispatch

- **Status:** Accepted
- **Date:** 2026-11-08
- **Design:** [request streaming](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/request-streaming.md)

## Context

`StreamAsync` returns an `IAsyncEnumerable<TItem>`, which has no single result to carry failures, and can be read more
than once. Decision 15 reports every failure of `SendAsync` through its result.

## Decision

`StreamAsync` never throws and does nothing when called. A null request, a missing handler, a canceled token, and
activation failures all surface from the first `MoveNextAsync` of a read. Every read starts a new dispatch, resolving
new components and running the pipeline again, as a C# `async` iterator method does.

## Alternatives considered

- **Validating the request and finding the handler when `StreamAsync` is called.** Rejected: two timings to learn,
  contrary to decision 15.
- **A stream that can be read only once.** Rejected: it differs from every `IAsyncEnumerable<T>` that C# iterators
  produce, and would need its own error.

## Consequences

- A stream that is created but never read costs nothing and has no effects.
- Reading a stream twice runs the handler twice.
- Errors surface where the stream is read, which may be far from where it was created.
