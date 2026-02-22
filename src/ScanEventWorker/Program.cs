using Amazon;
using Amazon.SQS;
using ScanEventWorker.Contracts;
using ScanEventWorker.Infrastructure.ApiClient;
using ScanEventWorker.Infrastructure.Messaging;
using ScanEventWorker.Infrastructure.Persistence;
using ScanEventWorker.Services;
using ScanEventWorker.Workers;

// Prevent multiple instances from running simultaneously (see Assumptions: single-instance constraint).
// The Mutex is held for the lifetime of the process; released automatically on process exit.
using var instanceMutex = new Mutex(initiallyOwned: true, name: "Global\\ScanEventWorker_Instance", out bool mutexAcquired);
if (!mutexAcquired)
{
    // Another instance is already running — log to stderr and exit cleanly.
    Console.Error.WriteLine("FATAL: Another ScanEventWorker instance is already running. Exiting.");
    return 1;
}

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configuration
string connectionString = builder.Configuration.GetConnectionString("ScanEvents")
                          ?? throw new InvalidOperationException("ConnectionStrings:ScanEvents is required");
string sqsQueueUrl = builder.Configuration["Aws:SqsQueueUrl"]
                     ?? throw new InvalidOperationException("Aws:SqsQueueUrl is required");


IConfigurationSection scanEventApiSection = builder.Configuration.GetSection("ScanEventApi");

var scanOptions = new ScanEventApiOptions
{
    BaseUrl = scanEventApiSection["BaseUrl"] ?? throw new InvalidOperationException("ScanEventApi:BaseUrl is required."),
    BatchSize = int.Parse(scanEventApiSection["BatchSize"] ?? "100"),
    PollingIntervalSeconds = int.Parse(scanEventApiSection["PollingIntervalSeconds"] ?? "5"),
    ErrorRetryIntervalSeconds = int.Parse(scanEventApiSection["ErrorRetryIntervalSeconds"] ?? "30"),
};

builder.Services.AddSingleton(scanOptions);

// Infrastructure — Persistence
builder.Services.AddSingleton(sp =>
    new DatabaseInitialiser(connectionString, sp.GetRequiredService<ILogger<DatabaseInitialiser>>()));
builder.Services.AddSingleton<IScanEventRepository>(sp =>
    new ScanEventRepository(
        connectionString,
        sp.GetRequiredService<ILogger<ScanEventRepository>>()));

// Infrastructure — HTTP API Client with resilience
builder.Services.AddHttpClient<IScanEventApiClient, ScanEventApiClient>(client =>
{
    string baseUrl = scanOptions.BaseUrl ?? throw new InvalidOperationException("ScanEventApi:BaseUrl is required");
    client.BaseAddress = new Uri(baseUrl);
}).AddStandardResilienceHandler();

// Infrastructure — SQS
builder.Services.AddSingleton<IAmazonSQS>(_ =>
{
    string region = builder.Configuration["Aws:Region"] ?? "ap-southeast-2";
    var config = new AmazonSQSConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(region) };

    string? serviceUrl = builder.Configuration["Aws:ServiceUrl"];
    if (!string.IsNullOrEmpty(serviceUrl))
    {
        config.ServiceURL = serviceUrl;
    }

    return new AmazonSQSClient(config);
});
builder.Services.AddSingleton<IMessageQueue>(sp =>
    new SqsMessageQueue(sp.GetRequiredService<IAmazonSQS>(), sqsQueueUrl));

// Services
builder.Services.AddSingleton<IScanEventProcessor, ScanEventProcessor>();

// Workers
builder.Services.AddHostedService<ApiPollerWorker>();
builder.Services.AddHostedService<EventProcessorWorker>();

IHost host = builder.Build();

// Initialise database schema on startup
DatabaseInitialiser dbInitialiser = host.Services.GetRequiredService<DatabaseInitialiser>();
await dbInitialiser.InitialiseAsync(CancellationToken.None);

host.Run();

return 0;
