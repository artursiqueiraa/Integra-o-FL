using System.Collections.Concurrent;
using CentralHub.SDK.Jfl.Server;

namespace CentralHub.Api.Logging;

/// <summary>
/// Buffer em memória (não persistido) das últimas entradas de log estruturado capturadas de
/// classes do SDK (via <see cref="SdkActivityLoggerProvider"/>) — alimenta o painel "Log da
/// Central" e os campos de sessão que <c>JflSession</c> não expõe publicamente (SEQ, bytes,
/// latência, último comando). Só assina o evento público
/// <see cref="SessionManager.SessaoRegistrada"/> (leitura), para resolver
/// <c>NumeroSerie</c> a partir de <c>RemoteEndPoint</c> nas entradas que não o carregam
/// diretamente (ex.: logs anteriores ao handshake terminar) — nunca chama nem altera nada
/// além disso no SDK.
/// </summary>
public sealed class SessionActivityLogService : IHostedService
{
    private const int CapacidadeMaxima = 500;

    private readonly SessionManager _sessionManager;
    private readonly ConcurrentQueue<AtividadeLogEntry> _entradas = new();
    private readonly ConcurrentDictionary<string, string> _numeroSeriePorRemoteEndPoint = new();
    private int _contagem;

    public SessionActivityLogService(SessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.SessaoRegistrada += OnSessaoRegistrada;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.SessaoRegistrada -= OnSessaoRegistrada;
        return Task.CompletedTask;
    }

    private void OnSessaoRegistrada(JflSession session)
    {
        if (session.NumeroSerie is not null)
        {
            _numeroSeriePorRemoteEndPoint[session.RemoteEndPoint] = session.NumeroSerie;
        }
    }

    /// <summary>Chamado pelo <see cref="SdkActivityLogger"/> para cada linha de log capturada.</summary>
    public void Registrar(AtividadeLogEntry entrada)
    {
        if (entrada.NumeroSerie is null && entrada.RemoteEndPoint is not null &&
            _numeroSeriePorRemoteEndPoint.TryGetValue(entrada.RemoteEndPoint, out var numeroSerieResolvido))
        {
            entrada = entrada with { NumeroSerie = numeroSerieResolvido };
        }

        _entradas.Enqueue(entrada);

        if (Interlocked.Increment(ref _contagem) > CapacidadeMaxima)
        {
            if (_entradas.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _contagem);
            }
        }
    }

    /// <summary>Últimas entradas relevantes para uma central, mais recente primeiro.</summary>
    public IReadOnlyList<AtividadeLogEntry> ObterPara(string numeroSerie, int max = 100) =>
        _entradas
            .Where(e => e.NumeroSerie == numeroSerie)
            .OrderByDescending(e => e.Timestamp)
            .Take(max)
            .ToList();
}
