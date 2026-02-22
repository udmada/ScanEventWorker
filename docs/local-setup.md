# Local Setup

## Prerequisites

- [mise](https://mise.jdx.dev/getting-started.html)
  - manages .NET 10, pkl, and hk
- [Docker](https://www.docker.com/)
  - for SQL Server + LocalStack

## Steps

### 1. Install tools

```bash
mise i
```

This installs .NET 10, pkl, and hk as declared in `mise.toml`.

### 2. Start infrastructure

```bash
docker-compose up -d
```

This starts:

- **Azure SQL Edge** on `localhost:1433` (ARM64-compatible SQL Server)
- **LocalStack** on `localhost:4566` (local SQS emulator - queues auto-created via `scripts/init-localstack.sh`)

The `ScanEvents` database and schema tables are auto-created by `DatabaseInitialiser` on first worker startup — no manual `sqlcmd` step required.

### 3. Set dummy AWS credentials (LocalStack doesn't validate them)

```bash
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
```

### 4. Configure the API base URL (required)

The worker cannot connect without this. Set it via user-secrets:

```bash
dotnet user-secrets set "ScanEventApi:BaseUrl" "https://your-api-host" \
  --project src/ScanEventWorker
```

Or edit `ScanEventApi:BaseUrl` directly in `src/ScanEventWorker/appsettings.json`.

### 5. Run the worker

```bash
dotnet run --project src/ScanEventWorker/ScanEventWorker.csproj
```

### 6. Verify resumability

Stop the worker (`Ctrl+C`) and restart it. The log will show:

```
Resuming from LastEventId=<n>
```

confirming it picks up from where it left off.
