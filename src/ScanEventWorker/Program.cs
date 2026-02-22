using Amazon.SQS;
using ScanEventWorker.Contracts;
using ScanEventWorker.Infrastructure.ApiClient;
using ScanEventWorker.Infrastructure.Messaging;
using ScanEventWorker.Infrastructure.Persistence;
using ScanEventWorker.Services;
using ScanEventWorker.Workers;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
var connectionString = builder.Configuration.GetConnectionString("ScanEvents")
    ?? throw new InvalidOperationException("ConnectionStrings:ScanEvents is required");
var sqsQueueUrl = builder.Configuration["Aws:SqsQueueUrl"]
    ?? throw new InvalidOperationException("Aws:SqsQueueUrl is required");

builder.Services.Configure<ScanEventApiOptions>(
    builder.Configuration.GetSection("ScanEventApi"));

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
    var baseUrl = builder.Configuration["ScanEventApi:BaseUrl"]
        ?? throw new InvalidOperationException("ScanEventApi:BaseUrl is required");
    client.BaseAddress = new Uri(baseUrl);
}).AddStandardResilienceHandler();

// Infrastructure — SQS
builder.Services.AddSingleton<IAmazonSQS>(_ =>
{
    var region = builder.Configuration["Aws:Region"] ?? "ap-southeast-2";
    var config = new AmazonSQSConfig { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region) };

    var serviceUrl = builder.Configuration["Aws:ServiceUrl"];
    if (!string.IsNullOrEmpty(serviceUrl))
        config.ServiceURL = serviceUrl;

    return new AmazonSQSClient(config);
});
builder.Services.AddSingleton<IMessageQueue>(sp =>
    new SqsMessageQueue(sp.GetRequiredService<IAmazonSQS>(), sqsQueueUrl));

// Services
builder.Services.AddSingleton<IScanEventProcessor, ScanEventProcessor>();

// Workers
builder.Services.AddHostedService<ApiPollerWorker>();
builder.Services.AddHostedService<EventProcessorWorker>();

var host = builder.Build();

// Initialize database schema on startup
var dbInitializer = host.Services.GetRequiredService<DatabaseInitializer>();
await dbInitializer.InitializeAsync(CancellationToken.None);

host.Run();
