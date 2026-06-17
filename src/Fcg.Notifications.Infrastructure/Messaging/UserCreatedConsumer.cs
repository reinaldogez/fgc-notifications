using Fcg.Contracts.Events;
using Fcg.Notifications.Application.Abstractions;
using Fcg.Notifications.Application.BoasVindas;
using MassTransit;

namespace Fcg.Notifications.Infrastructure.Messaging;

public class UserCreatedConsumer(IIdempotencyStore idempotency, EnviarBoasVindasHandler handler)
    : IConsumer<UserCreatedEvent>
{
    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        Guid messageId =
            context.MessageId
            ?? throw new InvalidOperationException("Mensagem sem MessageId — não deduplicável.");

        // SET NX: true = marquei agora (processa); false = já existia (redelivery, pula). ACK em ambos.
        if (!await idempotency.TryMarkAsync(messageId.ToString(), context.CancellationToken))
        {
            return;
        }

        UserCreatedEvent evt = context.Message;
        BoasVindasCommand command = new(evt.Email, evt.Name); // wire EN → Command PT-BR
        await handler.HandleAsync(command, context.CancellationToken);
    }
}
