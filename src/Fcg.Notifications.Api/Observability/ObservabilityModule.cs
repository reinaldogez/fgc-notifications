using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;

namespace Fcg.Notifications.Api.Observability;

public static class ObservabilityModule
{
    private const string ServiceName = "Fcg.Notifications.Api";
    private const string AppLabel = "fcg-notifications";

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        string? lokiUrl = builder.Configuration["Loki:Url"];
        string? otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        // Console e enricher de trace entram sempre; o sink Loki só com URL configurada.
        builder.Host.UseSerilog(
            (_, loggerConfig) =>
            {
                loggerConfig
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .Enrich.WithSpan()
                    .Enrich.WithProperty("service.name", ServiceName)
                    .WriteTo.Console();

                if (!string.IsNullOrWhiteSpace(lokiUrl))
                {
                    loggerConfig.WriteTo.GrafanaLoki(
                        lokiUrl,
                        labels: [new LokiLabel { Key = "app", Value = AppLabel }]
                    );
                }
            }
        );

        // OTLP (traces/metrics) só com endpoint configurado — folha do trace via fonte "MassTransit".
        if (!string.IsNullOrWhiteSpace(otelEndpoint))
        {
            builder
                .Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(ServiceName))
                .WithTracing(tracing =>
                    tracing
                        .AddAspNetCoreInstrumentation()
                        .AddSource("MassTransit")
                        .AddOtlpExporter(exporter => exporter.Endpoint = new Uri(otelEndpoint))
                )
                .WithMetrics(metrics =>
                    metrics
                        .AddAspNetCoreInstrumentation()
                        .AddOtlpExporter(exporter => exporter.Endpoint = new Uri(otelEndpoint))
                );
        }

        return builder;
    }
}
