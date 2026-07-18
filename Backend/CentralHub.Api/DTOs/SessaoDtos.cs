namespace CentralHub.Api.DTOs;

/// <summary>
/// Snapshot completo da sessão de uma Central — alimenta o card "Status da Conexão", o painel
/// "Sessão TCP" e o modal "Detalhes da Sessão" (mesmo recurso, vistas diferentes no Frontend).
/// A fonte de verdade de <see cref="StatusConexao"/> é sempre uma consulta ao vivo ao
/// SessionManager no momento da chamada — nunca a coluna <c>Central.Status</c> persistida, que
/// pode estar defasada.
/// </summary>
public class SessaoDto
{
    public int CentralId { get; set; }

    /// <summary>
    /// "Online" (sessão registrada no SessionManager) ou "Offline" (nenhuma sessão). O
    /// SessionManager só registra uma sessão *depois* do handshake concluído (0x21/0x2A) —
    /// não existe forma pública de observar "socket aberto, handshake ainda não concluído"
    /// sem tocar no SDK, então esse estado intermediário nunca aparece aqui. O Frontend usa
    /// "Aguardando conexão" (🟡) só como estado visual transitório logo após o botão
    /// Reconectar, antes do próximo polling confirmar Online/Offline — não é algo que o
    /// Backend calcula.
    /// </summary>
    public string StatusConexao { get; set; } = "Offline";

    // --- Dados de cadastro ---
    public string? NumeroSerie { get; set; }
    public string? Modelo { get; set; }
    public string? Firmware { get; set; }

    // --- Dados da sessão viva (SessionManager), null quando offline ---
    public string? IpSessao { get; set; }
    public int? PortaRemota { get; set; }
    public DateTime? DataHoraConexaoUtc { get; set; }
    public DateTime? UltimoPacoteRecebidoEmUtc { get; set; }
    public long? TempoConectadoSegundos { get; set; }
    public bool SocketConectado { get; set; }
    public bool HandshakeRealizado { get; set; }
    public bool KeepAliveAtivo { get; set; }
    public string? Mac { get; set; }
    public string? Imei { get; set; }

    // --- Histórico (banco), disponível mesmo offline ---
    public DateTime? UltimoKeepAliveEmUtc { get; set; }
    public string? UltimoIpConectado { get; set; }

    // --- Derivados do log de atividade capturado (SessionActivityLogService) ---
    public string? UltimoComando { get; set; }
    public byte? UltimoSeq { get; set; }
    public int? BytesRecebidos { get; set; }
    public int? BytesEnviados { get; set; }
    public double? LatenciaMs { get; set; }
    public string? UltimoErro { get; set; }

    // --- Indicadores (cálculo simples, para os chips da tela) ---
    public bool SessaoAtiva { get; set; }
    public bool CentralCadastrada { get; set; }
    public bool NumeroSerieDivergente { get; set; }
}

public class AtividadeLogEntryDto
{
    public required DateTimeOffset Timestamp { get; set; }
    public required string Nivel { get; set; }
    public required string Mensagem { get; set; }
    public byte? Cmd { get; set; }
    public byte? Seq { get; set; }
}

public class ReconectarResultDto
{
    public bool SessaoEncontrada { get; set; }
    public required string Mensagem { get; set; }
}

public class DiagnosticoItemDto
{
    public required string Descricao { get; set; }
    /// <summary><c>true</c>/<c>false</c> = resultado apurado; <c>null</c> = ainda sem dados suficientes para checar.</summary>
    public bool? Ok { get; set; }
    public string? Detalhe { get; set; }
}

public class DiagnosticoDto
{
    public int CentralId { get; set; }
    public required IReadOnlyList<DiagnosticoItemDto> Itens { get; set; }
}
