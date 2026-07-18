namespace CentralHub.SDK.Jfl.Server;

public sealed class JflServerOptions
{
    /// <summary>Porta TCP em que o servidor escuta. Use 0 para a porta ser escolhida pelo SO (util em testes).</summary>
    public int Porta { get; set; } = 8085;

    /// <summary>
    /// Intervalo de keep-alive (em minutos, 1 a 20) informado ao equipamento nas
    /// respostas dos comandos de conexao (0x21) e keep-alive (0x40).
    /// </summary>
    public byte IntervaloKeepAliveMinutos { get; set; } = 5;

    /// <summary>
    /// Liga o log opcional de RX/TX em hex+ASCII de cada conexao aceita (Fase 0.8 do plano de
    /// homologacao, via <see cref="CentralHub.SDK.Jfl.Diagnostics.HexLoggingStream"/>).
    /// Desligado por padrao — nao muda nenhum byte trafegado, so adiciona log em nivel Debug.
    /// </summary>
    public bool LogHexAtivado { get; set; }
}
