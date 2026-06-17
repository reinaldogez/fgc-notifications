using Fcg.Notifications.Application.Abstractions;
using Fcg.Notifications.Infrastructure.Idempotency;
using Fcg.Notifications.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Fcg.Notifications.Infrastructure;

public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        string redisConn = config["Redis:Connection"]!; // do Secret (Redis__Connection)
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConn)
        );
        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        services.AddMessaging(config); // MassTransit + consumers
        return services;
    }
}
