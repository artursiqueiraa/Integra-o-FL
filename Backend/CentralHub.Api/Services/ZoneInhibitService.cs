using CentralHub.Api.Data;
using CentralHub.Api.DTOs;
using CentralHub.SDK.Jfl.Messages.Status;
using CentralHub.SDK.Jfl.Server;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Services;

/// <summary>
/// Inibe/desinibe zonas de uma Central cadastrada. O comando real (0x52, via
/// <see cref="ZoneInhibitCommandService"/>) substitui o conjunto INTEIRO de zonas inibidas a
/// cada envio — não soma. Por isso este serviço primeiro consulta o estado atual (via
/// <see cref="CentralStatusQueryService"/>, o mesmo comando 0x4D já usado por
/// <see cref="CentralStatusService"/>), calcula o novo conjunto completo (atual + a zona
/// alvo, ou atual - a zona alvo) e só então envia o comando — nunca disca para fora.
/// </summary>
public class ZoneInhibitService
{
    private readonly AppDbContext _context;
    private readonly CentralStatusQueryService _statusQueryService;
    private readonly ZoneInhibitCommandService _zoneInhibitCommandService;
    private readonly ILogger<ZoneInhibitService> _logger;

    public ZoneInhibitService(
        AppDbContext context,
        CentralStatusQueryService statusQueryService,
        ZoneInhibitCommandService zoneInhibitCommandService,
        ILogger<ZoneInhibitService> logger)
    {
        _context = context;
        _statusQueryService = statusQueryService;
        _zoneInhibitCommandService = zoneInhibitCommandService;
        _logger = logger;
    }

    public Task<ZoneInhibitResultDto> InibirAsync(int centralId, int zona, CancellationToken cancellationToken) =>
        AlterarAsync(centralId, zona, inibir: true, cancellationToken);

    public Task<ZoneInhibitResultDto> DesinibirAsync(int centralId, int zona, CancellationToken cancellationToken) =>
        AlterarAsync(centralId, zona, inibir: false, cancellationToken);

    public async Task<IReadOnlyList<int>> ObterInibidasAsync(int centralId, CancellationToken cancellationToken)
    {
        var numeroSerie = await ResolverNumeroSerieAsync(centralId, cancellationToken);
        var statusAtual = await ConsultarStatusOuFalharAsync(numeroSerie, centralId, cancellationToken);
        return statusAtual.Zonas.Where(z => z.Estado == ZoneState.Inibida).Select(z => z.Numero).ToList();
    }

    private async Task<ZoneInhibitResultDto> AlterarAsync(int centralId, int zona, bool inibir, CancellationToken cancellationToken)
    {
        var numeroSerie = await ResolverNumeroSerieAsync(centralId, cancellationToken);
        var statusAtual = await ConsultarStatusOuFalharAsync(numeroSerie, centralId, cancellationToken);

        var zonasInibidas = statusAtual.Zonas.Where(z => z.Estado == ZoneState.Inibida).Select(z => z.Numero).ToHashSet();
        if (inibir)
        {
            zonasInibidas.Add(zona);
        }
        else
        {
            zonasInibidas.Remove(zona);
        }

        _logger.LogInformation(
            "Requisicao de {Acao} zona: CentralId={CentralId} NumeroSerie={NumeroSerie} Zona={Zona}",
            inibir ? "inibir" : "desinibir", centralId, numeroSerie, zona);

        var resultado = await _zoneInhibitCommandService.InibirZonasAsync(numeroSerie, zonasInibidas, cancellationToken);

        if (!resultado.Sucesso)
        {
            _logger.LogWarning(
                "Comando de inibir zonas falhou: CentralId={CentralId} Zona={Zona} Motivo={Motivo} Erro={Erro}",
                centralId, zona, resultado.Motivo, resultado.Erro);

            var statusCode = resultado.Motivo switch
            {
                ZoneInhibitFailureReason.CentralOffline => 409,
                ZoneInhibitFailureReason.NumeroInvalido => 400,
                _ => 502,
            };

            throw new BusinessException(resultado.Erro ?? "Falha ao enviar comando de inibir zonas.", statusCode);
        }

        var zonaResultante = resultado.ZonasResultantes!.FirstOrDefault(z => z.Numero == zona);
        var inibidaConfirmada = zonaResultante?.Estado == ZoneState.Inibida;

        _logger.LogInformation(
            "Comando de {Acao} zona concluido: CentralId={CentralId} Zona={Zona} InibidaConfirmada={InibidaConfirmada}",
            inibir ? "inibir" : "desinibir", centralId, zona, inibidaConfirmada);

        return new ZoneInhibitResultDto
        {
            Zona = zona,
            Sucesso = true,
            Inibida = inibidaConfirmada,
        };
    }

    private async Task<string> ResolverNumeroSerieAsync(int centralId, CancellationToken cancellationToken)
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

        return central.NumeroSerie;
    }

    private async Task<CentralStatusResponse> ConsultarStatusOuFalharAsync(string numeroSerie, int centralId, CancellationToken cancellationToken)
    {
        var resultado = await _statusQueryService.ConsultarAsync(numeroSerie, cancellationToken);
        if (!resultado.Sucesso)
        {
            _logger.LogWarning(
                "Falha ao consultar status para inibir zonas da Central {CentralId}: {Motivo} - {Erro}",
                centralId, resultado.Motivo, resultado.Erro);
            var statusCode = resultado.Motivo == CentralStatusQueryFailureReason.CentralOffline ? 409 : 502;
            throw new BusinessException(resultado.Erro ?? "Falha ao consultar status da central.", statusCode);
        }

        return resultado.Status!;
    }
}
