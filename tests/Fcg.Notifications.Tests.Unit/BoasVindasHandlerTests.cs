using Fcg.Notifications.Application.BoasVindas;
using FluentAssertions;
using Microsoft.Extensions.Logging.Testing;

namespace Fcg.Notifications.Tests.Unit;

public class BoasVindasHandlerTests
{
    [Fact]
    public async Task DeveLogarEmailDeBoasVindasComNomeEEmail()
    {
        FakeLogger<EnviarBoasVindasHandler> logger = new();
        EnviarBoasVindasHandler handler = new(logger);

        await handler.HandleAsync(
            new BoasVindasCommand("ana@fcg.com", "Ana"),
            CancellationToken.None
        );

        FakeLogRecord record = logger.Collector.GetSnapshot().Single();
        record.Message.Should().Contain("Ana").And.Contain("ana@fcg.com");
    }

    [Fact]
    public async Task DeveIncluirPrefixoEmailEmTodasAsMensagens()
    {
        FakeLogger<EnviarBoasVindasHandler> logger = new();

        await new EnviarBoasVindasHandler(logger).HandleAsync(
            new BoasVindasCommand("ana@fcg.com", "Ana"),
            CancellationToken.None
        );

        logger.Collector.GetSnapshot().Single().Message.Should().StartWith("[EMAIL]");
    }
}
