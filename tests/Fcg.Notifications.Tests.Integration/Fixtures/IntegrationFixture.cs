using Fcg.Notifications.Application;
using Fcg.Notifications.Infrastructure;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace Fcg.Notifications.Tests.Integration.Fixtures;

// Sobe Redis + RabbitMQ reais uma única vez e monta um host genérico com o wiring de produção
// (AddApplication + AddInfrastructure). O log é capturado por FakeLogCollector — sem Serilog no
// caminho, todos os records (handlers + bus) chegam ao coletor.
public sealed class IntegrationFixture : IAsyncLifetime
{
    private const string BrokerUser = "fcg";
    private const string BrokerPass = "fcg";

    private readonly RedisContainer _redis = new RedisBuilder("redis:7.4-alpine").Build();

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
        await Task.WhenAll(_redis.StartAsync(), _rabbit.StartAsync());

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Redis:Connection"] = _redis.GetConnectionString(),
                // URI completa (amqp://user:pass@host:porta-dinâmica): o Host(string) de produção
                // resolve host e porta a partir dela, sem mapear porta fixa no container.
                ["RabbitMq:Host"] = _rabbit.GetConnectionString(),
                ["RabbitMq:Username"] = BrokerUser,
                ["RabbitMq:Password"] = BrokerPass,
            }
        );

        builder.Logging.ClearProviders();
        builder.Logging.AddFakeLogging();
        // Os nomes de fila aparecem nos logs de topologia do bus, emitidos em Debug.
        builder.Logging.AddFilter("MassTransit", LogLevel.Debug);

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);

        // StartAsync só retorna com o bus pronto (filas declaradas/bound), senão a primeira
        // publicação pode se perder antes de o endpoint existir — flaky sob carga de Docker.
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
        await _redis.DisposeAsync();
    }

    // Espera até haver pelo menos 'atLeast' logs cujo texto contém o token único do teste (ou o
    // timeout). Devolve os logs casados — o consumo é assíncrono sobre um broker real.
    public async Task<IReadOnlyList<FakeLogRecord>> EsperarLogsAsync(
        string token,
        int atLeast,
        TimeSpan timeout
    )
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            List<FakeLogRecord> casados = LogsComToken(token);
            if (casados.Count >= atLeast)
            {
                return casados;
            }

            await Task.Delay(100);
        }

        return LogsComToken(token);
    }

    public List<FakeLogRecord> LogsComToken(string token) =>
        [.. Logs.GetSnapshot().Where(r => r.Message.Contains(token, StringComparison.Ordinal))];
}
