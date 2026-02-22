# ADR 001: ScanEventRepository - Integration Tests Only, No IDbConnection Factory

**Status:** Accepted
**Date:** 2026-02-21

## Context

The project uses Dapper.AOT with Native AOT (`PublishAot=true`). `ScanEventRepository` wraps a raw `SqlConnection` and contains non-trivial SQL logic: a MERGE-based upsert with an idempotency guard (`incoming.EventId > stored.LatestEventId`) and conditional timestamp writes for PICKUP/DELIVERY events.

The question arose: should we introduce an `IDbConnection` factory abstraction so that `ScanEventRepository` can be unit tested without a real database?

## Decision

Do not introduce an `IDbConnection` factory. Accept that `ScanEventRepository` and `DatabaseInitializer` require a real SQL Server instance to test. Document the gap in the README.

## Rationale

### The meaningful abstraction already exists

`IScanEventRepository` means every consumer - `ApiPollerWorker`, `EventProcessorWorker`, and `ScanEventProcessor` - can be fully tested with NSubstitute mocks without touching a database. The repository implementation being untestable without a DB is a much smaller problem than the workers being untestable.

### Mocking IDbConnection is unsafe under Dapper.AOT

Dapper.AOT generates source-level interceptors that target specific `SqlConnection` call sites at compile time. A mock `IDbConnection` would not trigger those interceptors - tests would execute different code paths than production, making them unreliable and potentially misleading.

### The SQL is the logic

The interesting correctness properties of `ScanEventRepository` - the MERGE semantics, the idempotency guard, the PICKUP/DELIVERY timestamp invariant - live in the SQL strings. No amount of `IDbConnection` mocking verifies that the SQL is correct. This is genuine integration-test territory.

### Deadline and risk

Introducing a new abstraction layer less than two days before the deadline risks destabilising the Native AOT build for low confidence in return.

## Alternatives Considered

| Alternative                                | Why rejected                                                        |
| ------------------------------------------ | ------------------------------------------------------------------- |
| Mock `IDbConnection`                       | Dapper.AOT interceptors won't fire; tests cover the wrong code      |
| Wrap `SqlConnection` in a custom interface | Same AOT interception problem; adds complexity with no real benefit |
| Switch to EF Core                          | EF Core does not support Native AOT; breaks the build constraint    |

## Consequences

- `ScanEventRepository` and `DatabaseInitializer` are covered by integration tests only (requiring a running SQL Server - see Docker Compose setup in README).
- All higher-level components (`ScanEventProcessor`, workers) remain fully unit-testable via `IScanEventRepository`.
- The README documents the integration-test gap.

## Unit Test Scope

Everything not in `ScanEventRepository` / `DatabaseInitializer` is unit tested:

| Target                              | Approach    | Mocks                                                          |
| ----------------------------------- | ----------- | -------------------------------------------------------------- |
| `Result<T>`                         | Pure unit   | None                                                           |
| `EventId`, `ParcelId` value objects | Pure unit   | None                                                           |
| `ScanEventProcessor`                | NSubstitute | `IScanEventRepository`, `IMessageQueue`                        |
| `ApiPollerWorker`                   | NSubstitute | `IScanEventApiClient`, `IMessageQueue`, `IScanEventRepository` |
| `EventProcessorWorker`              | NSubstitute | `IMessageQueue`, `IScanEventProcessor` (via interface/wrapper) |
| `SqsMessageQueue`                   | NSubstitute | `IAmazonSQS`                                                   |
