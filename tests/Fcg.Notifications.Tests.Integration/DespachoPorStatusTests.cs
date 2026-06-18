using Fcg.Contracts.Enums;
using Fcg.Contracts.Events;
using Fcg.Notifications.Tests.Integration.Fixtures;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Testing;

namespace Fcg.Notifications.Tests.Integration;

[Collection("Integration")]
public class DespachoPorStatusTests(IntegrationFixture fixture)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task DeveDespacharParaConfirmacaoQuandoApproved()
    {
        string token = Guid.NewGuid().ToString("N");
        string nomeJogo = $"Hades-{token}";
        PaymentProcessedEvent evt = new()
        {
            PaymentId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserEmail = "ana@fcg.com",
            UserName = "Ana",
            GameId = Guid.NewGuid(),
            GameName = nomeJogo,
            Status = PaymentStatus.Approved,
        };

        await fixture.Bus.Publish(
            evt,
            context => context.MessageId = Guid.NewGuid(),
            CancellationToken.None
        );

        IReadOnlyList<FakeLogRecord> logs = await fixture.EsperarLogsAsync(nomeJogo, 1, Timeout);

        logs.Should().ContainSingle();
        logs[0].Message.Should().Contain("Confirmação de compra").And.Contain(nomeJogo);
    }

    [Fact]
    public async Task DeveDespacharParaRecusaQuandoRejected()
    {
        string token = Guid.NewGuid().ToString("N");
        string motivo = $"Cartão recusado {token}";
        PaymentProcessedEvent evt = new()
        {
            PaymentId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserEmail = "ana@fcg.com",
            UserName = "Ana",
            GameId = Guid.NewGuid(),
            GameName = "Hades",
            Status = PaymentStatus.Rejected,
            RejectionReason = motivo,
        };

        await fixture.Bus.Publish(
            evt,
            context => context.MessageId = Guid.NewGuid(),
            CancellationToken.None
        );

        IReadOnlyList<FakeLogRecord> logs = await fixture.EsperarLogsAsync(token, 1, Timeout);

        logs.Should().ContainSingle();
        logs[0].Message.Should().Contain("Recusa de compra").And.Contain(motivo);
    }
}
