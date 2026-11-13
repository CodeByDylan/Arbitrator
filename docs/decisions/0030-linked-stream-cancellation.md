# 30. Stream cancellation links both tokens

- **Status:** Accepted
- **Date:** 2026-11-08
- **Design:** [request streaming](https://github.com/CodeByDylan/Arbitrator/blob/f2bb0a6673fa49c8666cd5d21febda9d0c81993b/docs/design/request-streaming.md)

## Context

A stream receives a token from `StreamAsync` and another when a read begins, through `GetAsyncEnumerator` or
`WithCancellation`. Decision 14 checks a send's token once, at entry.

## Decision

Both tokens are honored, exactly as `[EnumeratorCancellation]` combines them for C# iterators: when both are supplied
and differ, a linked token is created for the read and disposed when the read ends. That effective token is checked once,
after the handler lookup at the start of each read, and is then only forwarded. It is never checked between items.

## Alternatives considered

- **Only the `StreamAsync` token.** Rejected: a `WithCancellation` token would be ignored without notice.
- **Rejecting reads that supply both.** Rejected: both are legitimate, and combining them is the language's own rule.

## Consequences

- `await foreach` with `WithCancellation` behaves as callers expect.
- With two tokens, the token that components receive and that an entry `OperationCanceledException` carries is the
  linked one, not either caller's token.
