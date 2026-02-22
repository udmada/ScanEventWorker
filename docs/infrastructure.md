# Infrastructure

## CDK Stack

The `src/ScanEventWorker.Cdk` project defines the AWS infrastructure for production deployment:

```bash
cd src/ScanEventWorker.Cdk
cdk deploy
```

Provisions:

- `scan-events-queue` — main SQS queue (visibility timeout: 30s)
- `scan-events-dlq` — dead letter queue with `maxReceiveCount=3` redrive policy

> **Local development:** queues are created automatically by LocalStack when you run `docker-compose up` (via `scripts/init-localstack.sh`). You do not need to run `cdk deploy` locally.

## Downstream Workers Architecture

The current design is ready for fan-out without changes to this worker. Adding an SNS topic makes the event stream available to any number of downstream consumers:

```mermaid
flowchart LR
    POLLER["API Poller"]
    SNS(["SNS Topic\n(fan-out)"])
    QA[["SQS Queue A"]]
    QB[["SQS Queue B"]]
    QC[["SQS Queue C"]]
    W1["This Worker → DB"]
    W2["Downstream Worker 1"]
    W3["Downstream Worker 2"]

    POLLER --> SNS
    SNS --> QA --> W1
    SNS --> QB --> W2
    SNS --> QC --> W3
```

**Alternatives considered:**

- **Outbox pattern** — write events to an outbox table, dedicated publisher reads and publishes to SNS/SQS. Stronger consistency guarantee but adds DB coupling and latency.
- **CDC (Change Data Capture)** — enable SQL Server CDC on `ParcelSummary`, stream changes via Kafka Connect. Best for consumers that need the DB state, not the raw events.
