using CentralHub.Api.Data;
using CentralHub.Api.Models;
using CentralHub.SDK.Jfl.Protocol;
using CentralHub.SDK.Jfl.Server;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Services;

/// <summary>
/// Ouve o ciclo de vida das sessoes TCP do servidor JFL (eventos do SessionManager,
/// no SDK) e persiste um historico em CentralSession, vinculando pelo numero de
/// serie a uma Central cadastrada quando existir uma correspondente. O SDK
/// permanece agnostico de EF Core/SQLite — essa ponte fica no Backend.
/// </summary>
public class JflSessionPersistenceService : IHostedService
{
    private readonly SessionManager _sessionManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JflSessionPersistenceService> _logger;

    public JflSessionPersistenceService(SessionManager sessionManager, IServiceScopeFactory scopeFactory, ILogger<JflSessionPersistenceService> logger)
    {
        _sessionManager = sessionManager;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.SessaoRegistrada += OnSessaoRegistrada;
        _sessionManager.SessaoRemovida += OnSessaoRemovida;
        _sessionManager.AtividadeAtualizada += OnAtividadeAtualizada;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.SessaoRegistrada -= OnSessaoRegistrada;
        _sessionManager.SessaoRemovida -= OnSessaoRemovida;
        _sessionManager.AtividadeAtualizada -= OnAtividadeAtualizada;
        return Task.CompletedTask;
    }

    private void OnSessaoRegistrada(JflSession session) => _ = PersistirConexaoAsync(session);

    private void OnSessaoRemovida(JflSession session) => _ = PersistirDesconexaoAsync(session);

    private void OnAtividadeAtualizada(JflSession session) => _ = AtualizarUltimoKeepAliveAsync(session);

    private async Task PersistirConexaoAsync(JflSession session)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var central = await context.Centrals.FirstOrDefaultAsync(c => c.NumeroSerie == session.NumeroSerie);

            var registro = new CentralSession
            {
                NumeroSerie = session.NumeroSerie!,
                CentralId = central?.Id,
                Imei = session.Imei,
                Mac = session.Mac,
                Modelo = session.Modelo ?? 0,
                ModeloNome = session.Modelo?.ToNomeAmigavel(),
                VersaoFirmware = session.VersaoFirmware,
                EnderecoRemoto = session.RemoteEndPoint,
                Status = CentralSessionStatus.Conectada,
                ConectadaEmUtc = session.ConectadoEmUtc.UtcDateTime,
                UltimoKeepAliveEmUtc = session.UltimaAtividadeUtc.UtcDateTime,
            };

            context.CentralSessions.Add(registro);

            if (central is not null)
            {
                central.Status = "Online";
                central.Fabricante ??= "JFL";
                central.Modelo = session.Modelo?.ToNomeAmigavel() ?? central.Modelo;
                central.Firmware = session.VersaoFirmware ?? central.Firmware;
                central.UltimoIpConectado = session.RemoteIp ?? central.UltimoIpConectado;
                central.UltimoKeepAliveEmUtc = session.UltimaAtividadeUtc.UtcDateTime;
                central.ConectadoDesdeUtc = session.ConectadoEmUtc.UtcDateTime;
            }
            else
            {
                _logger.LogWarning(
                    "Central com numero de serie {NumeroSerie} conectou ao servidor JFL mas nao esta cadastrada",
                    session.NumeroSerie);
            }

            await context.SaveChangesAsync();
            _logger.LogInformation(
                "Sessao persistida para central {NumeroSerie} (CentralId={CentralId})", session.NumeroSerie, central?.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao persistir conexao da central {NumeroSerie}", session.NumeroSerie);
        }
    }

    private async Task PersistirDesconexaoAsync(JflSession session)
    {
        if (session.NumeroSerie is null)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var registro = await context.CentralSessions
                .Where(s => s.NumeroSerie == session.NumeroSerie && s.Status == CentralSessionStatus.Conectada)
                .OrderByDescending(s => s.ConectadaEmUtc)
                .FirstOrDefaultAsync();

            if (registro is not null)
            {
                registro.Status = CentralSessionStatus.Desconectada;
                registro.DesconectadaEmUtc = DateTime.UtcNow;
            }

            var central = await context.Centrals.FirstOrDefaultAsync(c => c.NumeroSerie == session.NumeroSerie);
            if (central is not null)
            {
                central.Status = "Offline";
                central.ConectadoDesdeUtc = null;
            }

            await context.SaveChangesAsync();
            _logger.LogInformation("Desconexao persistida para central {NumeroSerie}", session.NumeroSerie);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao persistir desconexao da central {NumeroSerie}", session.NumeroSerie);
        }
    }

    private async Task AtualizarUltimoKeepAliveAsync(JflSession session)
    {
        if (session.NumeroSerie is null)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var registro = await context.CentralSessions
                .Where(s => s.NumeroSerie == session.NumeroSerie && s.Status == CentralSessionStatus.Conectada)
                .OrderByDescending(s => s.ConectadaEmUtc)
                .FirstOrDefaultAsync();

            if (registro is not null)
            {
                registro.UltimoKeepAliveEmUtc = session.UltimaAtividadeUtc.UtcDateTime;

                if (registro.CentralId is int centralId)
                {
                    var central = await context.Centrals.FindAsync(centralId);
                    if (central is not null)
                    {
                        central.UltimoKeepAliveEmUtc = registro.UltimoKeepAliveEmUtc;
                        central.UltimoIpConectado = session.RemoteIp ?? central.UltimoIpConectado;
                    }
                }

                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao atualizar atividade da central {NumeroSerie}", session.NumeroSerie);
        }
    }
}
