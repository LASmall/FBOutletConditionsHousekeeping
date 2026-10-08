using Azure.Monitor.OpenTelemetry.Exporter;
using FBOutletConditionsHousekeeping.Core;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

// The Functions Worker SDK 2.x unified hosting model requires
// ConfigureFunctionsWebApplication() as the entry point even for apps with
// no HTTP-triggered functions (this app has only a timer trigger).
builder.ConfigureFunctionsWebApplication();

builder.Services.AddCleanupServices(builder.Configuration);

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

builder.Build().Run();
