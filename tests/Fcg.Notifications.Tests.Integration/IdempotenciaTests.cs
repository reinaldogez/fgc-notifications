using Fcg.Contracts.Events;
using Fcg.Notifications.Tests.Integration.Fixtures;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Testing;

namespace Fcg.Notifications.Tests.Integration;

[Collection("Integration")]
public class IdempotenciaTests(IntegrationFixture fixture)
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task DeveProcessarMensagemNovaUmaVez()
    {
        string token = Guid.NewGuid().ToString("N");
        UserCreatedEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            Email = $"{token}@fcg.com",
            Name = "Ana",
        };

        await fixture.Bus.Publish(
            evt,
            context => context.MessageId = Guid.NewGuid(),
            CancellationToken.None
        );

        IReadOnlyList<FakeLogRecord> logs = await fixture.EsperarLogsAsync(token, 1, s_timeout);

        logs.Should().ContainSingle();
        logs[0].Message.Should().StartWith("[EMAIL]").And.Contain("Boas-vindas");
    }

    [Fact]
    public async Task DeveIgnorarRedeliveryDaMesmaMensagem()
    {
        string token = Guid.NewGuid().ToString("N");
        var messageId = Guid.NewGuid();
        UserCreatedEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            Email = $"{token}@fcg.com",
            Name = "Ana",
        };

        // Mesma mensagem (mesmo MessageId) publicada duas vezes = a redelivery do broker.
        await fixture.Bus.Publish(
            evt,
            context => context.MessageId = messageId,
            CancellationToken.None
        );
        await fixture.Bus.Publish(
            evt,
            context => context.MessageId = messageId,
            CancellationToken.None
        );

        await fixture.EsperarLogsAsync(token, 1, s_timeout);
        // Carência para garantir que a segunda entrega, se fosse processada, já teria logado.
        await Task.Delay(TimeSpan.FromSeconds(2));

        fixture
            .LogsComToken(token)
            .Should()
            .ContainSingle("a dedup por MessageId garante um efeito por mensagem");
    }
}
