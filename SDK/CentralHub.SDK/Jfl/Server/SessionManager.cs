using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server;

/// <summary>
/// Registro thread-safe das sessoes ativas, indexadas pelo numero de serie do
/// equipamento. E aqui que uma futura implementacao de negocio (armar, PGM, zonas)
/// vai localizar a sessao TCP ja aberta de uma central especifica para enviar um
/// comando dentro dela — nao existe (nem deve existir) discagem de saida.
/// </summary>
public sealed class SessionManager
{
    private readonly ConcurrentDictionary<string, JflSession> _sessoesPorNumeroSerie = new();
    private readonly ILogger<SessionManager> _logger;

    public SessionManager(ILogger<SessionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>Disparado quando uma sessao termina o handshake e passa a ficar disponivel para comandos.</summary>
    public event Action<JflSession>? SessaoRegistrada;

    /// <summary>Disparado quando uma sessao e removida (desconexao ou substituicao por reconexao).</summary>
    public event Action<JflSession>? SessaoRemovida;

    /// <summary>Disparado quando a sessao atualmente registrada para um numero de serie tem atividade nova.</summary>
    public event Action<JflSession>? AtividadeAtualizada;

    public int QuantidadeAtiva => _sessoesPorNumeroSerie.Count;

    public IReadOnlyCollection<JflSession> Sessoes => _sessoesPorNumeroSerie.Values.ToList();

    public bool TryGet(string numeroSerie, out JflSession? session) =>
        _sessoesPorNumeroSerie.TryGetValue(numeroSerie, out session);

    public void Registrar(JflSession session)
    {
        if (string.IsNullOrEmpty(session.NumeroSerie))
        {
            throw new InvalidOperationException("Sessao precisa ter NumeroSerie definido antes de ser registrada.");
        }

        if (_sessoesPorNumeroSerie.TryGetValue(session.NumeroSerie, out var existente) && existente.Id != session.Id)
        {
            _logger.LogWarning(
                "Central {NumeroSerie} reconectou de {NovoEndpoint}; encerrando sessao anterior de {AntigoEndpoint}",
                session.NumeroSerie, session.RemoteEndPoint, existente.RemoteEndPoint);

            existente.Close();
            Remover(existente);
        }

        _sessoesPorNumeroSerie[session.NumeroSerie] = session;
        _logger.LogInformation("Sessao registrada: central {NumeroSerie} ({RemoteEndPoint})", session.NumeroSerie, session.RemoteEndPoint);
        SessaoRegistrada?.Invoke(session);
    }

    public void Remover(JflSession session)
    {
        if (session.NumeroSerie is null)
        {
            return;
        }

        if (_sessoesPorNumeroSerie.TryGetValue(session.NumeroSerie, out var atual) && atual.Id == session.Id)
        {
            _sessoesPorNumeroSerie.TryRemove(session.NumeroSerie, out _);
            _logger.LogInformation("Sessao removida: central {NumeroSerie}", session.NumeroSerie);
            SessaoRemovida?.Invoke(session);
        }
    }

    /// <summary>
    /// Notifica atividade (tipicamente um keep-alive) para a sessao atualmente
    /// registrada. Nao dispara nada se a sessao informada ja foi substituida por
    /// outra reconexao com o mesmo numero de serie.
    /// </summary>
    public void NotificarAtividade(JflSession session)
    {
        if (session.NumeroSerie is null)
        {
            return;
        }

        if (_sessoesPorNumeroSerie.TryGetValue(session.NumeroSerie, out var atual) && atual.Id == session.Id)
        {
            AtividadeAtualizada?.Invoke(session);
        }
    }
}
