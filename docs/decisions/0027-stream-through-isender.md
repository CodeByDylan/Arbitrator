# 27. Streams are sent through `ISender.StreamAsync`

- **Status:** Accepted
- **Date:** 2026-11-08
- **Design:** [request streaming](../design/request-streaming.md)

## Context

A stream request has exactly one handler, like any other request. The entry point needs a home and a name.

## Decision

`ISender` gains `StreamAsync<TItem>(IStreamRequest<TItem>, CancellationToken)`, which returns `IAsyncEnumerable<TItem>`.
The `Async` suffix follows decision 1 and the .NET convention for methods that return a stream, such as
`ChannelReader<T>.ReadAllAsync` and `File.ReadLinesAsync`.

## Alternatives considered

- **`CreateStream`.** Rejected: it breaks the naming rule of decision 1.
- **A separate scoped `IStreamSender`.** Rejected: a third service to inject for what is still sending a request.
  `IPublisher` is separate because notifications follow different rules, and stream requests don't.

## Consequences

- Adding a member to `ISender` breaks classes that implement it, typically test fakes. Version 0.x allows this, and the
  release notes list it.
- Code that already injects `ISender` can stream without new dependencies.
