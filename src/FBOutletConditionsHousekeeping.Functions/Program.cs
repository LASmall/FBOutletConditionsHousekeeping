using FBOutletConditionsHousekeeping.Core;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// The Functions Worker SDK 2.x unified hosting model requires
// ConfigureFunctionsWebApplication() as the entry point even for apps with
// no HTTP-triggered functions (this app has only a timer trigger).
builder.ConfigureFunctionsWebApplication();

builder.Services.AddCleanupServices(builder.Configuration);

// No explicit Application Insights wiring is needed here: with host.json's
// "telemetryMode" set to "applicationInsights" (the classic, non-OpenTelemetry
// mode), the Functions host itself automatically records every invocation
// (any trigger type, including the timer trigger) and forwards ILogger
// output, as long as APPLICATIONINSIGHTS_CONNECTION_STRING is configured.
// A prior OpenTelemetry-based setup here only wired up tracing (never
// logging) and did not produce invocation telemetry for the timer trigger
// — confirmed via a live run showing 0 invocations in the Portal despite
// the function executing successfully.
builder.Build().Run();
