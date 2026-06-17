namespace Fcg.Notifications.Application.Abstractions;

/// <summary>
/// Porta de idempotência: marca uma mensagem como processada de forma atômica
/// (<c>SET NX EX</c>), para descartar redeliveries do broker.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Marca a mensagem como processada de forma atômica (<c>SET NX EX</c>).
    /// </summary>
    /// <param name="messageId">Identificador único da mensagem (<c>MessageId</c>).</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>
    /// <see langword="true"/> = marcada agora (primeira vez — <b>deve processar</b>);
    /// <see langword="false"/> = já existia (redelivery — <b>deve pular</b>).
    /// </returns>
    /// <remarks>
    /// Lança se o store estiver indisponível (postura "dependência dura"):
    /// a exceção sobe e o transporte faz NACK/redelivery, em vez de processar às cegas.
    /// </remarks>
    Task<bool> TryMarkAsync(string messageId, CancellationToken ct);
}
