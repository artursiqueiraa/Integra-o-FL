using CentralHub.Api.Data;
using CentralHub.Api.DTOs;
using CentralHub.SDK.Jfl.Messages.Status;
using CentralHub.SDK.Jfl.Server;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Services;

/// <summary>
/// Consulta o status ao vivo de uma Central cadastrada: resolve o NumeroSerie no
/// banco, delega ao <see cref="CentralStatusQueryService"/> do SDK (que usa a
/// sessao TCP ja aberta pela central, via SessionManager) e mapeia o resultado
/// tipado do protocolo para os DTOs da API.
/// </summary>
public class CentralStatusService
{
    private readonly AppDbContext _context;
    private readonly CentralStatusQueryService _queryService;
    private readonly ILogger<CentralStatusService> _logger;

    public CentralStatusService(AppDbContext context, CentralStatusQueryService queryService, ILogger<CentralStatusService> logger)
    {
        _context = context;
        _queryService = queryService;
        _logger = logger;
    }

    public async Task<CentralStatusDto> ConsultarStatusAsync(int centralId, CancellationToken cancellationToken)
    {
        var central = await _context.Centrals.FirstOrDefaultAsync(c => c.Id == centralId, cancellationToken);
        if (central is null)
        {
            throw new BusinessException($"Central {centralId} não encontrada.", statusCode: 404);
        }

        if (string.IsNullOrEmpty(central.NumeroSerie))
        {
            throw new BusinessException(
                $"Central {centralId} não possui Número de Série cadastrado; não é possível localizar a sessão da central.",
                statusCode: 409);
        }

        var resultado = await _queryService.ConsultarAsync(central.NumeroSerie, cancellationToken);

        if (!resultado.Sucesso)
        {
            _logger.LogWarning("Falha ao consultar status da Central {CentralId}: {Motivo} - {Erro}", centralId, resultado.Motivo, resultado.Erro);

            // 409 (Conflict) para central offline: o recurso existe (a Central esta
            // cadastrada), mas o estado atual dela (sem sessao ativa) conflita com a
            // operacao pedida. 502 para falhas de comunicacao com uma sessao que
            // existe mas nao respondeu a tempo/respondeu de forma invalida.
            var statusCode = resultado.Motivo == CentralStatusQueryFailureReason.CentralOffline ? 409 : 502;
            throw new BusinessException(resultado.Erro ?? "Falha ao consultar status da central.", statusCode);
        }

        return ParaDto(centralId, resultado.Status!);
    }

    private static CentralStatusDto ParaDto(int centralId, CentralStatusResponse status) => new()
    {
        CentralId = centralId,
        DataHoraCentral = status.DataHoraCentral,
        Bateria = new BateriaDto
        {
            ValorBruto = status.Bateria.ValorBruto,
            Tipo = status.Bateria.Tipo.ToString(),
            PercentualLitio = status.Bateria.PercentualLitio,
            TensaoChumboAproximada = status.Bateria.TensaoChumboAproximada,
        },
        Eletrificador = new EletrificadorDto
        {
            Estado = status.Eletrificador.Estado?.ToString(),
            PermiteDesarmar = status.Eletrificador.PermiteDesarmar,
            PermiteArmarAway = status.Eletrificador.PermiteArmarAway,
        },
        Problemas = new ProblemasDto
        {
            BateriaFracaControleOuSensorSemFio = status.Problemas.BateriaFracaControleOuSensorSemFio,
            SupervisaoSensor = status.Problemas.SupervisaoSensor,
            SaidaAuxiliar = status.Problemas.SaidaAuxiliar,
            Tamper = status.Problemas.Tamper,
            Dhcp = status.Problemas.Dhcp,
            CaboDeRede = status.Problemas.CaboDeRede,
            ModuloCelular = status.Problemas.ModuloCelular,
            Sms = status.Problemas.Sms,
            Ethernet = status.Problemas.Ethernet,
            Gprs = status.Problemas.Gprs,
            LinhaTelefonica = status.Problemas.LinhaTelefonica,
            Curto = status.Problemas.Curto,
            Teclado = status.Problemas.Teclado,
            Sirene = status.Problemas.Sirene,
            Bateria = status.Problemas.Bateria,
            Ac = status.Problemas.Ac,
            BateriaInvertidaOuEmCurto = status.Problemas.BateriaInvertidaOuEmCurto,
            IpDestino2 = status.Problemas.IpDestino2,
            IpDestino1 = status.Problemas.IpDestino1,
            ServidorDns = status.Problemas.ServidorDns,
            RedeTecladoAc = status.Problemas.RedeTecladoAc,
            SupervisaoSirene = status.Problemas.SupervisaoSirene,
            SenhaRedeSemFio = status.Problemas.SenhaRedeSemFio,
            AutenticacaoRedeSemFio = status.Problemas.AutenticacaoRedeSemFio,
            SsidNaoEncontrado = status.Problemas.SsidNaoEncontrado,
            ConflitoIp = status.Problemas.ConflitoIp,
            Barramento = status.Problemas.Barramento,
            Ddns = status.Problemas.Ddns,
            Notificacao = status.Problemas.Notificacao,
            ModuloEthernet = status.Problemas.ModuloEthernet,
            NivelSinalOperadora = status.Problemas.NivelSinalOperadora,
            ChipCelular = status.Problemas.ChipCelular,
            TamperTeclado = status.Problemas.TamperTeclado,
            SupervisaoPgm = status.Problemas.SupervisaoPgm,
            AlimentacaoAcNormal = status.Problemas.AlimentacaoAcNormal,
        },
        Particoes = status.Particoes.Select(p => new ParticaoStatusDto
        {
            Numero = p.Numero,
            Estado = p.Estado?.ToString(),
            Desabilitada = p.Desabilitada,
            PermiteDesarmar = p.PermiteDesarmar,
            PermiteArmar = p.PermiteArmar,
            PermiteArmarStay = p.PermiteArmarStay,
            PermiteArmarAway = p.PermiteArmarAway,
            Pronta = p.Pronta,
        }).ToList(),
        Zonas = status.Zonas.Select(z => new ZonaStatusDto
        {
            Numero = z.Numero,
            Estado = z.Estado?.ToString(),
            PermiteInibir = z.PermiteInibir,
        }).ToList(),
        Pgms = status.Pgms.Select(p => new PgmStatusDto
        {
            Numero = p.Numero,
            Acionada = p.Acionada,
            Permitida = p.Permitida,
        }).ToList(),
    };
}
