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

### 3. Create the database

```bash
docker exec -it $(docker ps -q -f ancestor=mcr.microsoft.com/azure-sql-edge) \
  /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P 'YourStr0ngPassw0rd!' \
  -Q "CREATE DATABASE ScanEvents"
```

The worker auto-creates the `ProcessingState` and `ParcelSummary` tables on startup via `DatabaseInitialiser`.

### 4. Set dummy AWS credentials (LocalStack doesn't validate them)

```bash
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
```

### 5. Configure the API base URL (required)

The worker cannot connect without this. Set it via user-secrets:

```bash
dotnet user-secrets set "ScanEventApi:BaseUrl" "https://your-api-host" \
  --project src/ScanEventWorker
```

Or edit `ScanEventApi:BaseUrl` directly in `src/ScanEventWorker/appsettings.json`.

### 6. Run the worker

```bash
dotnet run --project src/ScanEventWorker/ScanEventWorker.csproj
```

### 7. Verify resumability

Stop the worker (`Ctrl+C`) and restart it. The log will show:

```
Resuming from LastEventId=<n>
```

confirming it picks up from where it left off.
