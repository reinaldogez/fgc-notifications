using Fcg.Notifications.Api.Observability;
using Fcg.Notifications.Application;
using Fcg.Notifications.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(); // handlers transientes
builder.Services.AddInfrastructure(builder.Configuration); // Redis + MassTransit + consumers

// Redis é a dependência dura → entra no /ready; o broker fica fora do ready.
builder
    .Services.AddHealthChecks()
    .AddRedis(builder.Configuration["Redis:Connection"]!, name: "redis", tags: ["ready"]);

builder.AddObservability(); // Console + enricher sempre; Loki/OTLP só por config

WebApplication app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// O ready reflete só o Redis (dependência dura). O check do bus, auto-registrado pelo MassTransit
// com a tag "ready", fica de fora — o broker é informativo, não bloqueante.
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions { Predicate = check => check.Name == "redis" }
);

await app.RunAsync();
