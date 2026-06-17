using Fcg.Notifications.Application.BoasVindas;
using Microsoft.Extensions.DependencyInjection;

namespace Fcg.Notifications.Application;

public static class ApplicationModule
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<EnviarBoasVindasHandler>();
        return services;
    }
}
