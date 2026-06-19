using Fcg.Notifications.Application;
using Fcg.Notifications.Infrastructure;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Testcontainers.RabbitMq;

namespace Fcg.Notifications.Tests.Integration.Fixtures;

// RabbitMQ próprio (não o compartilhado, para não virar consumidor concorrente da mesma fila) e um
// Redis inalcançável de propósito: o consumer falha ao marcar idempotência e nunca chega ao handler.
public sealed class ResilienciaFixture : IAsyncLifetime
{
    private const string BrokerUser = "fcg";
    private const string BrokerPass = "fcg";

    // abortConnect padrão (true): o Connect falha rápido em vez de esperar o timeout de comando a
    // cada retry, então o fault aparece dentro da janela do teste.
    private const string RedisMorto = "localhost:6390,connectTimeout=500,connectRetry=0";

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder(
        "rabbitmq:3.13-management-alpine"
    )
        .WithUsername(BrokerUser)
        .WithPassword(BrokerPass)
        .Build();

    private IHost? _host;

    public IBus Bus => _host!.Services.GetRequiredService<IBus>();

    public FakeLogCollector Logs => _host!.Services.GetRequiredService<FakeLogCollector>();

    public async Task InitializeAsync()
    {
        await _rabbit.StartAsync();

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Redis:Connection"] = RedisMorto,
                ["RabbitMq:Host"] = _rabbit.GetConnectionString(),
                ["RabbitMq:Username"] = BrokerUser,
                ["RabbitMq:Password"] = BrokerPass,
            }
        );

        builder.Logging.ClearProviders();
        builder.Logging.AddFakeLogging();
        builder.Logging.AddFilter("MassTransit", LogLevel.Debug);

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);

        // StartAsync só retorna com o bus pronto (fila declarada/bound), senão a primeira (e única)
        // publicação pode se perder antes do endpoint existir e a mensagem nunca falhar.
        builder.Services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(60);
        });

        _host = builder.Build();
        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        await _rabbit.DisposeAsync();
    }

    public List<FakeLogRecord> LogsComToken(string token) =>
        [.. Logs.GetSnapshot().Where(r => r.Message.Contains(token, StringComparison.Ordinal))];

    // Espera o consumer falhar (MassTransit loga a exceção em Error) ou o timeout. O retry curto
    // configurado (3 × 2s) atrasa o fault, então a janela precisa ser folgada.
    public async Task<bool> EsperarFaultAsync(TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Logs.GetSnapshot().Any(r => r.Level == LogLevel.Error))
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }
}
