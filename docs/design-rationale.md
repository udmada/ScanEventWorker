# Design Rationale

This document covers the architectural decisions and patterns in the codebase, with particular attention to where DDD and FP principles were applied deliberately.

## Rich Domain Model

`ParcelSummary` is not a data bag. Its `ApplyScanEvent()` method encapsulates three business rules in one place: idempotency (events with `EventId <= LatestEventId` are silently ignored, making the MERGE-based persistence safe for at-least-once SQS delivery), first-occurrence timestamp protection (`PickedUpAtUtc ??= ...` and `DeliveredAtUtc ??= ...` ensure a later pickup event after delivery never overwrites the original), and event-type semantics (the `switch` on `ScanEventTypes` is the sole location in the codebase that interprets what an event type means). `ScanEventProcessor` is deliberately thin: it delegates business rules to the domain object and wraps infrastructure errors in `Result<T>`.

## Railway-Oriented Error Handling

`Result<T>` is a `readonly struct` with `Success`/`Failure` factory methods and a `Match()` combinator. It is used at every infrastructure boundary: `IScanEventApiClient.GetScanEventsAsync` returns parse and HTTP failures as values rather than throwing; `IScanEventProcessor.ProcessSingleAsync` wraps database errors without propagating them; `ScanEventProcessor.ProcessBatchAsync` processes each event independently so one failure does not abort the batch. Exceptions are reserved for genuinely unrecoverable failures such as missing configuration on startup.

## Decoupled Workers via SQS

`ApiPollerWorker` and `EventProcessorWorker` communicate exclusively through SQS, which provides several properties the design relies on. Fault isolation: a database outage halts `EventProcessorWorker` without affecting `ApiPollerWorker`; events accumulate in the queue and drain automatically on recovery. At-least-once delivery: SQS redelivers unacknowledged messages after the visibility timeout, and the idempotent MERGE in `ApplyScanEvent()` makes redelivery safe. Dead-letter handling: after three failed attempts, SQS moves messages to the DLQ for manual inspection with no bespoke retry logic required. Independent scaling: multiple `EventProcessorWorker` instances can compete for messages without coordination.

## Contracts Over Concretions

All cross-cutting dependencies are defined as interfaces in `ScanEventWorker.Contracts`: `IScanEventApiClient`, `IScanEventRepository`, `IMessageQueue`, and `IScanEventProcessor`. The boundaries are chosen at architectural seams, not merely for test convenience. `IMessageQueue` hides the SQS SDK behind a three-method surface; `IScanEventProcessor` separates orchestration (the worker) from business logic (the processor). This lets the BackgroundService tests use NSubstitute mocks with zero real infrastructure.

## AOT as a Design Constraint

Native AOT (`PublishAot=true`) prohibits runtime reflection, which shaped several decisions. JSON serialisation uses `[JsonSerializable]` source-gen contexts rather than `JsonSerializer` with runtime type discovery. Data access uses Dapper.AOT with the `[DapperAot]` attribute and interceptor-based source generation instead of EF Core. Value objects (`EventId`, `ParcelId`) are `readonly record struct` types: zero heap allocation, comparable by value, and fully AOT-safe. The constraint made the codebase more explicit at the cost of additional boilerplate, but also eliminated whole categories of runtime surprises.
