using Amazon;
using Amazon.SQS;
using ScanEventWorker.Contracts;
using ScanEventWorker.Infrastructure.ApiClient;
using ScanEventWorker.Infrastructure.Messaging;
using ScanEventWorker.Infrastructure.Persistence;
using ScanEventWorker.Services;
using ScanEventWorker.Workers;

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
    new DatabaseInitializer(connectionString, sp.GetRequiredService<ILogger<DatabaseInitializer>>()));
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

// Initialize database schema on startup
DatabaseInitializer dbInitializer = host.Services.GetRequiredService<DatabaseInitializer>();
await dbInitializer.InitializeAsync(CancellationToken.None);

host.Run();
