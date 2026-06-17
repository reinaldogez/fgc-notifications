using Fcg.Contracts.Enums;
using Fcg.Contracts.Events;
using Fcg.Notifications.Application.Abstractions;
using Fcg.Notifications.Application.ConfirmacaoCompra;
using Fcg.Notifications.Application.RecusaCompra;
using MassTransit;

namespace Fcg.Notifications.Infrastructure.Messaging;

public class PaymentProcessedConsumer(
    IIdempotencyStore idempotency,
    EnviarConfirmacaoHandler confirmacao,
    EnviarRecusaHandler recusa
) : IConsumer<PaymentProcessedEvent>
{
    public async Task Consume(ConsumeContext<PaymentProcessedEvent> context)
    {
        Guid messageId =
            context.MessageId
            ?? throw new InvalidOperationException("Mensagem sem MessageId — não deduplicável.");

        if (!await idempotency.TryMarkAsync(messageId.ToString(), context.CancellationToken))
        {
            return;
        }

        PaymentProcessedEvent evt = context.Message;
        CancellationToken ct = context.CancellationToken;

        switch (evt.Status)
        {
            case PaymentStatus.Approved:
                await confirmacao.HandleAsync(
                    new ConfirmacaoCompraCommand(
                        evt.UserEmail,
                        evt.UserName,
                        evt.GameName,
                        evt.OrderId
                    ),
                    ct
                );
                break;
            case PaymentStatus.Rejected:
                await recusa.HandleAsync(
                    new RecusaCompraCommand(
                        evt.UserEmail,
                        evt.UserName,
                        evt.GameName,
                        evt.OrderId,
                        evt.RejectionReason ?? "Motivo não informado"
                    ),
                    ct
                );
                break;
            default:
                throw new InvalidOperationException(
                    $"Status de pagamento não suportado: {evt.Status}"
                );
        }
    }
}
