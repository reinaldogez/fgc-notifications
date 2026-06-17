using Fcg.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fcg.Notifications.Infrastructure.Messaging;

public static class MassTransitConfig
{
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        services.AddMassTransit(x =>
        {
            x.AddConsumer<UserCreatedConsumer>();
            x.AddConsumer<PaymentProcessedConsumer>();

            x.UsingRabbitMq(
                (ctx, cfg) =>
                {
                    cfg.Host(
                        config["RabbitMq:Host"]!,
                        h =>
                        {
                            h.Username(config["RabbitMq:Username"]!);
                            h.Password(config["RabbitMq:Password"]!);
                        }
                    );

                    // nome da exchange por mensagem — contrato puro, nome vive no bus
                    cfg.Message<UserCreatedEvent>(m => m.SetEntityName("user-created"));
                    cfg.Message<PaymentProcessedEvent>(m => m.SetEntityName("payment-processed"));

                    // nome da fila explícito por consumer (sufixo .fcg-notifications)
                    cfg.ReceiveEndpoint(
                        "user-created.fcg-notifications",
                        e =>
                        {
                            e.ConfigureConsumer<UserCreatedConsumer>(ctx);
                            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));
                        }
                    );
                    cfg.ReceiveEndpoint(
                        "payment-processed.fcg-notifications",
                        e =>
                        {
                            e.ConfigureConsumer<PaymentProcessedConsumer>(ctx);
                            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));
                        }
                    );
                }
            );
        });
        return services;
    }
}
