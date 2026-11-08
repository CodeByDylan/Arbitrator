# 26. Dedicated streaming contracts

- **Status:** Accepted
- **Date:** 2026-11-08
- **Design:** [request streaming](../design/request-streaming.md)

## Context

A request typed `IRequest<IAsyncEnumerable<T>>` already dispatches, but behaviors wrap only the creation of the stream.
They finish before the first item is read, and never see an exception raised while items are produced. Applications
that need incremental results through the pipeline, such as logging the whole read or translating errors, can't get
them.

## Decision

Streaming has its own contracts: `IStreamRequest<TItem>`, `IStreamRequestHandler<TRequest, TItem>`,
`IStreamPipelineBehavior<TRequest, TItem>`, and the `StreamContinuation<TItem>` delegate. Handlers and behaviors return
`IAsyncEnumerable<TItem>`, and a stream behavior wraps the whole read. Request behaviors never wrap stream handlers, and
stream behaviors never wrap other handlers. Principle 1 becomes "one pipeline abstraction for each kind of request".

## Alternatives considered

- **Documenting `IRequest<IAsyncEnumerable<T>>`.** Rejected, because behaviors can't observe the read.
- **Letting `IPipelineBehavior` wrap streams by special-casing an `IAsyncEnumerable<T>` response.** Rejected: one
  interface would have two meanings, decided by the response type at runtime.

## Consequences

- Four new public types, and a second pipeline abstraction.
- C# forbids `yield return` inside a `try` block that has a `catch` clause (CS1626), so a stream behavior that catches
  exceptions while yielding reads its enumerator by hand.
- `IRequest<IAsyncEnumerable<T>>` keeps working unchanged, with its behaviors wrapping only the creation.
