# 31. A continuation's stream is read at most once

- **Status:** Accepted
- **Date:** 2026-11-08
- **Design:** [request streaming](../design/request-streaming.md)

## Context

Decision 7 allows `next` to be invoked at most once, because running the handler twice duplicates side effects. For a
stream behavior, invoking `next` once but reading the returned stream twice runs the handler twice as well.

## Decision

The stream a `StreamContinuation<TItem>` returns may be read once. A second `GetAsyncEnumerator` call throws
`InvalidOperationException` naming the behavior and the request type. The guard applies only to starting a read, so it
adds no cost per item.

## Alternatives considered

- **No guard.** Rejected: it leaves a gap in decision 7 for streams, and adding a guard later would be breaking.

## Consequences

- Retrying a stream inside the pipeline isn't possible, as for other requests.
- The stream a caller gets from `StreamAsync` isn't guarded: each of its reads is a new dispatch (decision 29).
