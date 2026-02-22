# ADR 002: No Saga Pattern

**Status:** Accepted
**Date:** 2026-02-22

## Context

The pipeline has two sequential step chains that look like distributed transactions on the surface:

- `ApiPollerWorker`: Poll API → Send to SQS → Advance `LastEventId`
- `EventProcessorWorker`: Receive from SQS → Merge to DB → Delete message

Saga came up as a way to handle mid-sequence failures.

## Decision

Don't use it. The design already handles failures correctly without a coordinator.

## Why

If `SendToSqs` fails, `LastEventId` doesn't advance - the next poll re-covers those events.
If `MergeToDb` fails, the message isn't deleted - SQS redelivers via visibility timeout.

Both chains self-recover because the MERGE is idempotent:

```sql
incoming.EventId > stored.LatestEventId
```

Replaying the same event is a no-op. Saga exists to handle cases where replay is _not_ safe. That case doesn't exist here.

## What Saga Would Have Cost

- An orchestration state machine
- Explicit compensation actions per step
- A coordinator to manage transitions

All of that is already implicit in `LastEventId` + SQS visibility timeout. Adding Saga would be solving a problem we don't have.

## If the Architecture Were Event-Driven

This decision is specific to the current poll-based design. In an event-driven model the calculus changes.

Each scan event arriving could naturally produce side-effects across multiple systems - a row written to DynamoDB for real-time lookup, a record appended to S3 for compliance archiving, a downstream notification, an audit trail. Each of those writes is a discrete step with its own failure mode. That's exactly the problem Saga is designed for: coordinating a sequence of steps across systems where partial failure needs explicit handling.

AWS Step Functions would carry most of the weight here - the state machine, retry policies, compensation routing, and execution history are all provided out of the box. The idempotency invariant still applies and still protects against duplicate executions, but Saga would earn its complexity cost in that design.

The current poll-based single-DB design doesn't have that fan-out problem, so Saga adds nothing today.

## Note on Test Cancellation

The `CancellationToken` complexity in the worker tests is a test harness concern - how to drive a `BackgroundService` loop one iteration at a time. It's unrelated to this decision.
