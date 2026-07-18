using CentralHub.Api.Data;
using CentralHub.Api.DTOs;
using CentralHub.Api.Logging;
using CentralHub.SDK.Jfl.Protocol;
using CentralHub.SDK.Jfl.Server;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Services;

/// <summary>
/// Monta o snapshot de sessão (Status da Conexão / Sessão TCP / Detalhes da Sessão), o log de
/// atividade e o diagnóstico de uma Central — toda informação vem do <c>SessionManager</c> (via
/// <c>NumeroSerie</c>) e do <see cref="SessionActivityLogService"/>. Nunca abre socket algum;
/// "Reconectar" só limpa a sessão registrada (equivalente ao que já acontece quando a central
/// cai sozinha), nunca disca para fora.
/// </summary>
public class CentralSessionService
{
    /// <summary>Mesma folga de 1,5x usada pelo protocolo oficial da JFL para tolerância de keep-alive.</summary>
    private const double FatorToleranciaKeepAlive = 1.5;

    private readonly AppDbContext _context;
    private readonly SessionManager _sessionManager;
    private readonly SessionActivityLogService _logService;
    private readonly JflServerOptions _jflOptions;
    private readonly ILogger<CentralSessionService> _logger;

    public CentralSessionService(
        AppDbContext context,
        SessionManager sessionManager,
        SessionActivityLogService logService,
        JflServerOptions jflOptions,
        ILogger<CentralSessionService> logger)
    {
        _context = context;
        _sessionManager = sessionManager;
        _logService = logService;
        _jflOptions = jflOptions;
        _logger = logger;
    }

    public async Task<SessaoDto> ObterSessaoAsync(int centralId, CancellationToken cancellationToken)
    {
        var central = await _context.Centrals.FirstOrDefaultAsync(c => c.Id == centralId, cancellationToken)
            ?? throw new BusinessException($"Central {centralId} não encontrada.", statusCode: 404);

        var dto = new SessaoDto
        {
            CentralId = central.Id,
            NumeroSerie = central.NumeroSerie,
            Modelo = central.Modelo,
            Firmware = central.Firmware,
            UltimoKeepAliveEmUtc = central.UltimoKeepAliveEmUtc,
            UltimoIpConectado = central.UltimoIpConectado,
            CentralCadastrada = true,
        };

        JflSession? sessao = null;
        if (!string.IsNullOrEmpty(central.NumeroSerie))
        {
            _sessionManager.TryGet(central.NumeroSerie, out sessao);
        }

        if (sessao is not null)
        {
            var agora = DateTimeOffset.UtcNow;
            var toleranciaKeepAlive = TimeSpan.FromMinutes(_jflOptions.IntervaloKeepAliveMinutos * FatorToleranciaKeepAlive);

            dto.StatusConexao = "Online";
            dto.SessaoAtiva = true;
            dto.SocketConectado = true;
            dto.HandshakeRealizado = true;
            dto.IpSessao = sessao.RemoteIp;
            dto.PortaRemota = ExtrairPorta(sessao.RemoteEndPoint);
            dto.DataHoraConexaoUtc = sessao.ConectadoEmUtc.UtcDateTime;
            dto.UltimoPacoteRecebidoEmUtc = sessao.UltimaAtividadeUtc.UtcDateTime;
            dto.TempoConectadoSegundos = (long)(agora - sessao.ConectadoEmUtc).TotalSeconds;
            dto.KeepAliveAtivo = agora - sessao.UltimaAtividadeUtc <= toleranciaKeepAlive;
            dto.Mac = sessao.Mac;
            dto.Imei = sessao.Imei;
            dto.NumeroSerieDivergente = sessao.NumeroSerie != central.NumeroSerie;

            // Preferir a sessao viva (JflSession.Modelo/VersaoFirmware) ao dado persistido no
            // banco: JflSessionPersistenceService grava esses campos de forma assincrona
            // (fire-and-forget) ao registrar a sessao, entao o valor do banco pode estar
            // atrasado ou ainda nao gravado no exato momento desta consulta.
            if (sessao.Modelo is byte modeloBruto)
            {
                dto.Modelo = modeloBruto.ToNomeAmigavel();
            }

            if (!string.IsNullOrEmpty(sessao.VersaoFirmware))
            {
                dto.Firmware = sessao.VersaoFirmware;
            }
        }
        else
        {
            dto.StatusConexao = "Offline";
        }

        if (!string.IsNullOrEmpty(central.NumeroSerie))
        {
            // Um "comando" completo normalmente gera duas linhas de log adjacentes (pedido:
            // Cmd+BytesEnviados; resposta: Seq+BytesRecebidos+TempoMs) — cada campo é buscado
            // independentemente na entrada mais recente que o carrega, não presos à mesma linha.
            var ultimasEntradas = _logService.ObterPara(central.NumeroSerie, max: 20);
            var ultimoComando = ultimasEntradas.FirstOrDefault(e => e.Cmd is not null);
            var ultimoSeq = ultimasEntradas.FirstOrDefault(e => e.Seq is not null);
            var ultimaComTempo = ultimasEntradas.FirstOrDefault(e => e.TempoRespostaMs is not null);
            var ultimaComBytesRecebidos = ultimasEntradas.FirstOrDefault(e => e.BytesRecebidos is not null);
            var ultimaComBytesEnviados = ultimasEntradas.FirstOrDefault(e => e.BytesEnviados is not null);
            var ultimoErro = ultimasEntradas.FirstOrDefault(e => e.Nivel >= LogLevel.Warning);

            if (ultimoComando is not null)
            {
                dto.UltimoComando = DescreverComando(ultimoComando.Cmd!.Value);
            }

            dto.UltimoSeq = ultimoSeq?.Seq;

            dto.LatenciaMs = ultimaComTempo?.TempoRespostaMs;
            dto.BytesRecebidos = ultimaComBytesRecebidos?.BytesRecebidos;
            dto.BytesEnviados = ultimaComBytesEnviados?.BytesEnviados;
            dto.UltimoErro = ultimoErro?.Mensagem;
        }

        return dto;
    }

    public async Task<IReadOnlyList<AtividadeLogEntryDto>> ObterLogAsync(int centralId, int max, CancellationToken cancellationToken)
    {
        var central = await _context.Centrals.FirstOrDefaultAsync(c => c.Id == centralId, cancellationToken)
            ?? throw new BusinessException($"Central {centralId} não encontrada.", statusCode: 404);

        if (string.IsNullOrEmpty(central.NumeroSerie))
        {
            return [];
        }

        return _logService.ObterPara(central.NumeroSerie, max)
            .Select(e => new AtividadeLogEntryDto
            {
                Timestamp = e.Timestamp,
                Nivel = e.Nivel.ToString(),
                Mensagem = e.Cmd is not null ? DescreverComando(e.Cmd.Value) : e.Mensagem,
                Cmd = e.Cmd,
                Seq = e.Seq,
            })
            .ToList();
    }

    /// <summary>
    /// Não abre nenhuma conexão — só limpa a sessão registrada (mesmo efeito de a central cair
    /// sozinha). Se a central mantiver o comportamento normal do protocolo, ela reconecta por
    /// conta própria (keep-alive/retry já documentados no protocolo oficial).
    /// </summary>
    public async Task<ReconectarResultDto> ReconectarAsync(int centralId, CancellationToken cancellationToken)
    {
        var central = await _context.Centrals.FirstOrDefaultAsync(c => c.Id == centralId, cancellationToken)
            ?? throw new BusinessException($"Central {centralId} não encontrada.", statusCode: 404);

        if (string.IsNullOrEmpty(central.NumeroSerie) || !_sessionManager.TryGet(central.NumeroSerie, out var sessao) || sessao is null)
        {
            return new ReconectarResultDto
            {
                SessaoEncontrada = false,
                Mensagem = "Nenhuma sessão ativa encontrada — a central já está aguardando conexão.",
            };
        }

        _logger.LogInformation("Reconectar solicitado manualmente para a central {NumeroSerie} (Id={CentralId})", central.NumeroSerie, centralId);

        sessao.Close();
        _sessionManager.Remover(sessao);

        return new ReconectarResultDto
        {
            SessaoEncontrada = true,
            Mensagem = "Sessão encerrada. A central deverá iniciar uma nova conexão automaticamente.",
        };
    }

    public async Task<DiagnosticoDto> ObterDiagnosticoAsync(int centralId, CancellationToken cancellationToken)
    {
        var central = await _context.Centrals
            .Include(c => c.Building)
            .FirstOrDefaultAsync(c => c.Id == centralId, cancellationToken)
            ?? throw new BusinessException($"Central {centralId} não encontrada.", statusCode: 404);

        JflSession? sessao = null;
        if (!string.IsNullOrEmpty(central.NumeroSerie))
        {
            _sessionManager.TryGet(central.NumeroSerie, out sessao);
        }

        var itens = new List<DiagnosticoItemDto>
        {
            new() { Descricao = "Sessão ativa", Ok = sessao is not null },
            new() { Descricao = "Handshake realizado", Ok = sessao is not null },
        };

        if (sessao is not null)
        {
            var tolerancia = TimeSpan.FromMinutes(_jflOptions.IntervaloKeepAliveMinutos * FatorToleranciaKeepAlive);
            var dentroDoPrazo = DateTimeOffset.UtcNow - sessao.UltimaAtividadeUtc <= tolerancia;
            itens.Add(new DiagnosticoItemDto
            {
                Descricao = "Keep-alive dentro do prazo",
                Ok = dentroDoPrazo,
                Detalhe = $"Tolerância: {tolerancia.TotalMinutes:F0} min (1,5x o intervalo configurado)",
            });
        }
        else
        {
            itens.Add(new DiagnosticoItemDto { Descricao = "Keep-alive dentro do prazo", Ok = null, Detalhe = "Sem sessão ativa" });
        }

        itens.Add(new DiagnosticoItemDto
        {
            Descricao = "Número de Série cadastrado",
            Ok = !string.IsNullOrEmpty(central.NumeroSerie),
        });

        itens.Add(new DiagnosticoItemDto
        {
            Descricao = "Central vinculada a um Prédio",
            Ok = central.Building is not null,
            Detalhe = central.Building?.Nome,
        });

        if (!string.IsNullOrEmpty(central.NumeroSerie))
        {
            var ultimaConsultaStatus = _logService
                .ObterPara(central.NumeroSerie, max: 50)
                .FirstOrDefault(e => e.Categoria.Contains("CentralStatusQueryService", StringComparison.Ordinal));

            itens.Add(new DiagnosticoItemDto
            {
                Descricao = "Última consulta de Status bem-sucedida",
                Ok = ultimaConsultaStatus is not null,
                Detalhe = ultimaConsultaStatus is not null ? $"{ultimaConsultaStatus.Timestamp:HH:mm:ss}" : "Sem consultas registradas nesta sessão",
            });
        }
        else
        {
            itens.Add(new DiagnosticoItemDto { Descricao = "Última consulta de Status bem-sucedida", Ok = null, Detalhe = "Sem Número de Série cadastrado" });
        }

        return new DiagnosticoDto { CentralId = centralId, Itens = itens };
    }

    private static int? ExtrairPorta(string remoteEndPoint)
    {
        var indice = remoteEndPoint.LastIndexOf(':');
        if (indice < 0 || indice == remoteEndPoint.Length - 1)
        {
            return null;
        }

        return int.TryParse(remoteEndPoint[(indice + 1)..], out var porta) ? porta : null;
    }

    private static string DescreverComando(byte cmd) => cmd switch
    {
        0x21 or 0x2A => "Handshake (Conexão)",
        0x40 => "KeepAlive",
        0x4D => "Status solicitado",
        0x4E => "Armar",
        0x4F => "Desarmar",
        0x50 => "PGM Acionar",
        0x51 => "PGM Desacionar",
        0x52 => "Inibir Zonas",
        0x53 => "Armar Stay",
        0x54 => "Armar Away",
        0x55 => "Atualizar Data/Hora",
        0x24 => "Evento",
        _ => $"Comando 0x{cmd:X2}",
    };
}
