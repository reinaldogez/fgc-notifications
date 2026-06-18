using Fcg.Notifications.Tests.Integration.Fixtures;
using FluentAssertions;

namespace Fcg.Notifications.Tests.Integration;

[Collection("Integration")]
public class FilasTests(IntegrationFixture fixture)
{
    [Fact]
    public void DeveCriarFilasComNomesExatos()
    {
        // Os endpoints declarados no boot do bus aparecem nos logs de topologia do MassTransit.
        IEnumerable<string> mensagens = fixture.Logs.GetSnapshot().Select(r => r.Message);

        mensagens
            .Should()
            .Contain(m => m.Contains("user-created.fcg-notifications", StringComparison.Ordinal))
            .And.Contain(m =>
                m.Contains("payment-processed.fcg-notifications", StringComparison.Ordinal)
            );
    }
}
