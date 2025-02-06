using System.Text.Json;
using DBChat.ApiService;
using DBChat.ApiService.Model;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

var deploymentName = builder.Configuration["AzureOpenAI:DeploymentName"];
var apiKey = builder.Configuration["AzureOpenAI:ApiKey"];
var endpoint = builder.Configuration["AzureOpenAI:Endpoint"];
var otelExporterEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
var otelExporterHeaders = builder.Configuration["OTEL_EXPORTER_OTLP_HEADERS"];

if (string.IsNullOrEmpty(deploymentName)) throw new InvalidOperationException("Deployment Name is required.");
if (string.IsNullOrEmpty(apiKey)) throw new InvalidOperationException("OpenAIKey is required.");
if (string.IsNullOrEmpty(endpoint)) throw new InvalidOperationException("Endpoint is required.");

AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);

var loggerFactory = LoggerFactory.Create(builder =>
{
    // Add OpenTelemetry as a logging provider
    builder.AddOpenTelemetry(options =>
    {
        options.AddOtlpExporter(exporter => {exporter.Endpoint = new Uri(otelExporterEndpoint); exporter.Headers = otelExporterHeaders; exporter.Protocol = OtlpExportProtocol.Grpc;});
        // Format log messages. This defaults to false.
        options.IncludeFormattedMessage = true;
    });

    builder.AddTraceSource("Microsoft.SemanticKernel");
    builder.SetMinimumLevel(LogLevel.Information);
});

using var traceProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Microsoft.SemanticKernel*")
    .AddOtlpExporter(exporter => {exporter.Endpoint = new Uri(otelExporterEndpoint); exporter.Headers = otelExporterHeaders; exporter.Protocol = OtlpExportProtocol.Grpc;})
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("Microsoft.SemanticKernel*")
    .AddOtlpExporter(exporter => {exporter.Endpoint = new Uri(otelExporterEndpoint); exporter.Headers = otelExporterHeaders; exporter.Protocol = OtlpExportProtocol.Grpc;})
    .Build();

builder.Services.AddTransient(builder => {
    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.Services.AddSingleton(loggerFactory);
    
    // return kernelBuilder.AddOpenAIChatCompletion("gpt-4o", apiKey, httpClient: httpClient).Build();
    return kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey).Build();
});

builder.Services.AddTransient<ChatWithDBAgent>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

app.MapPost("/api/chat/stream", async (ChatWithDBAgent agent, AIChatRequest request, HttpResponse response) =>
{
    var history = new ChatHistory();
    foreach(var message in request.Messages)
    {
        var role = message.Role == AIChatRole.Assistant ? AuthorRole.Assistant : AuthorRole.User;
        history.Add(new ChatMessageContent(role, message.Content));
    }

    response.Headers.Append("Content-Type", "application/jsonl");
    var chatResponse = agent.InvokeStreamingAsync(history);
    await foreach (var delta in chatResponse)
    {
        await response.WriteAsync($"{JsonSerializer.Serialize(new AIChatCompletionDelta(new AIChatMessageDelta() { Content = delta.Content }))}\r\n");
        await response.Body.FlushAsync();
    }
});

app.MapDefaultEndpoints();

app.Run();