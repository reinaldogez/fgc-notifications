using Microsoft.Extensions.Logging;

namespace Fcg.Notifications.Application.BoasVindas;

public class EnviarBoasVindasHandler(ILogger<EnviarBoasVindasHandler> logger)
{
    public Task HandleAsync(BoasVindasCommand command, CancellationToken ct)
    {
        logger.LogInformation(
            "[EMAIL] Boas-vindas\nPara: {Email}\nOlá, {Nome}! Sua conta na FCG foi criada com sucesso.",
            command.Email,
            command.Nome
        );
        return Task.CompletedTask;
    }
}
