using Microsoft.Extensions.Logging;

namespace CentralHub.Api.Logging;

/// <summary>
/// Uma entrada de log estruturado capturada de uma classe do SDK (via
/// <see cref="SdkActivityLoggerProvider"/>) — nunca inventada: cada campo vem de uma
/// propriedade nomeada que a própria chamada <c>_logger.LogX("...{Prop}...", valor)</c> já
/// emitia antes desta captura existir. Campos nulos significam apenas que aquela chamada de
/// log específica não carregava aquela propriedade — não é erro.
/// </summary>
public sealed record AtividadeLogEntry
{
    public required DateTimeOffset Timestamp { get; init; }

    public required LogLevel Nivel { get; init; }

    /// <summary>Nome completo da classe do SDK que gerou o log (ex.: "CentralHub.SDK.Jfl.Server.JflTcpServer").</summary>
    public required string Categoria { get; init; }

    /// <summary>Mensagem já formatada (pronta para exibir), exatamente como o SDK a gerou.</summary>
    public required string Mensagem { get; init; }

    public string? RemoteEndPoint { get; init; }

    public string? NumeroSerie { get; init; }

    public byte? Cmd { get; init; }

    public byte? Seq { get; init; }

    public int? BytesRecebidos { get; init; }

    public int? BytesEnviados { get; init; }

    public double? TempoRespostaMs { get; init; }
}
