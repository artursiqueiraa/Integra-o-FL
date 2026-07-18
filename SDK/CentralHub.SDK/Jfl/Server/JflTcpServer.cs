using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CentralHub.SDK.Jfl.Diagnostics;
using CentralHub.SDK.Jfl.Server.Handlers;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server;

/// <summary>
/// Servidor TCP compativel com o modelo de comunicacao da JFL: a central e quem
/// disca para fora, este processo apenas escuta e aceita. Cada conexao aceita vira
/// uma <see cref="JflSession"/> de longa duracao; pacotes recebidos sao roteados
/// pelo <see cref="JflCommandDispatcher"/> ate um handler.
/// </summary>
public sealed class JflTcpServer : IAsyncDisposable
{
    private readonly JflServerOptions _options;
    private readonly JflCommandDispatcher _dispatcher;
    private readonly SessionManager _sessionManager;
    private readonly ILogger<JflTcpServer> _logger;
    private readonly ConcurrentDictionary<Guid, Task> _handlersAtivos = new();

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loopDeAceitacao;

    public JflTcpServer(
        JflServerOptions options,
        JflCommandDispatcher dispatcher,
        SessionManager sessionManager,
        ILogger<JflTcpServer> logger)
    {
        _options = options;
        _dispatcher = dispatcher;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>Porta efetivamente em uso apos <see cref="Start"/> (util quando <see cref="JflServerOptions.Porta"/> e 0).</summary>
    public int Port { get; private set; }

    public bool EstaEmExecucao => _listener is not null;

    public void Start()
    {
        if (_listener is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _options.Porta);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _logger.LogInformation("Servidor JFL escutando na porta {Port}", Port);

        _loopDeAceitacao = AceitarConexoesAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();
        _listener?.Stop();

        try
        {
            if (_loopDeAceitacao is not null)
            {
                await _loopDeAceitacao.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // esperado ao cancelar o loop de aceitacao.
        }

        var pendentes = _handlersAtivos.Values.ToArray();
        await Task.WhenAll(pendentes).ConfigureAwait(false);

        _listener = null;
        _logger.LogInformation("Servidor JFL parado (porta {Port} liberada)", Port);
    }

    private async Task AceitarConexoesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Instrumentacao de homologacao: marca no log o instante exato em que o
            // loop volta a bloquear esperando a proxima conexao TCP.
            _logger.LogDebug("Aguardando nova conexao TCP na porta {Port}...", Port);

            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Loop de aceitacao cancelado (encerramento do servidor)");
                break;
            }
            catch (ObjectDisposedException)
            {
                _logger.LogDebug("Listener foi descartado (encerramento do servidor)");
                break;
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "Erro de socket ao aceitar conexao");
                continue;
            }

            // Instrumentacao de homologacao: log imediatamente apos o
            // AcceptTcpClientAsync() retornar, antes de qualquer outro processamento,
            // com IP e porta remotos separados como campos estruturados.
            var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
            _logger.LogInformation(
                "Conexao TCP aceita: IP remoto={RemoteIp} Porta remota={RemotePort}",
                remoteEndPoint?.Address, remoteEndPoint?.Port);

            var session = CriarSessao(client);
            var handlerTask = HandleClientAsync(session, cancellationToken);
            _handlersAtivos[session.Id] = handlerTask;
            _ = handlerTask.ContinueWith(
                completedTask => _handlersAtivos.TryRemove(session.Id, out _),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        _logger.LogDebug("Loop de aceitacao de conexoes encerrado");
    }

    /// <summary>
    /// Equivalente a <see cref="JflSession.FromTcpClient"/> (mesma extracao de stream/endpoint),
    /// mas envolvendo opcionalmente o stream num <see cref="HexLoggingStream"/> transparente
    /// quando <see cref="JflServerOptions.LogHexAtivado"/> esta ligado (Fase 0.8 do plano de
    /// homologacao) — unico ponto de integracao do log HEX; nao muda nenhum byte trafegado.
    /// </summary>
    private JflSession CriarSessao(TcpClient client)
    {
        var networkStream = client.GetStream();
        var ipEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
        var remoteEndPoint = ipEndPoint?.ToString() ?? client.Client.RemoteEndPoint?.ToString() ?? "desconhecido";
        var remoteIp = ipEndPoint?.Address.ToString();

        Stream stream = _options.LogHexAtivado
            ? new HexLoggingStream(networkStream, _logger, remoteEndPoint)
            : networkStream;

        return new JflSession(stream, remoteEndPoint, client, remoteIp);
    }

    private async Task HandleClientAsync(JflSession session, CancellationToken serverCancellationToken)
    {
        _logger.LogInformation("Nova conexao TCP recebida de {RemoteEndPoint}", session.RemoteEndPoint);

        // Instrumentacao de homologacao: sinaliza com destaque (Information) o
        // primeiro pacote decodificado com sucesso nesta sessao — e o sinal mais
        // importante para confirmar que a central conseguiu falar com o servidor.
        var primeiroPacoteRecebido = false;

        try
        {
            while (!serverCancellationToken.IsCancellationRequested)
            {
                var pacote = await session.ReceiveAsync(serverCancellationToken).ConfigureAwait(false);
                if (pacote is null)
                {
                    _logger.LogInformation(
                        "Conexao encerrada pelo equipamento: central {NumeroSerie} ({RemoteEndPoint})",
                        session.NumeroSerie ?? "desconhecida", session.RemoteEndPoint);
                    break;
                }

                session.MarcarAtividade();

                if (!primeiroPacoteRecebido)
                {
                    primeiroPacoteRecebido = true;
                    _logger.LogInformation(
                        "Primeiro comando recebido de {RemoteEndPoint}: Cmd=0x{Cmd:X2} Seq=0x{Seq:X2} DadosLength={DadosLength}",
                        session.RemoteEndPoint, pacote.Cmd, pacote.Seq, pacote.Dados.Length);
                }

                // Instrumentacao de homologacao: bytes totais do pacote no fio
                // (framing CAB+QDE+SEQ+CMD+K = 5 bytes + o tamanho de Dados).
                _logger.LogDebug(
                    "Pacote recebido de {RemoteEndPoint}: {Pacote} BytesRecebidos={BytesRecebidos}",
                    session.RemoteEndPoint, pacote, pacote.Dados.Length + 5);

                if (session.TryCompletePendingRequest(pacote))
                {
                    // Resposta correlacionada a um comando iniciado pelo servidor
                    // (ex.: 0x4D aguardando via SendAndWaitAsync) — nao repassa ao
                    // dispatcher de comandos normais.
                    continue;
                }

                try
                {
                    await _dispatcher.DispatchAsync(session, pacote, serverCancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex, "Erro ao processar comando 0x{Cmd:X2} da central {NumeroSerie}",
                        pacote.Cmd, session.NumeroSerie ?? "desconhecida");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // esperado durante o encerramento do servidor.
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Conexao com {RemoteEndPoint} (central {NumeroSerie}) perdida", session.RemoteEndPoint, session.NumeroSerie ?? "desconhecida");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado na sessao de {RemoteEndPoint}", session.RemoteEndPoint);
        }
        finally
        {
            // Instrumentacao de homologacao: log incondicional de fechamento —
            // roda para todo caminho de saida do loop acima (peer fechou, excecao,
            // ou cancelamento do servidor), garantindo que toda conexao aceita
            // tenha um log correspondente de encerramento.
            _logger.LogInformation(
                "Conexao finalizada: {RemoteEndPoint} (central {NumeroSerie}, primeiro comando recebido={PrimeiroComandoRecebido})",
                session.RemoteEndPoint, session.NumeroSerie ?? "desconhecida", primeiroPacoteRecebido);

            _sessionManager.Remover(session);
            session.Close();
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
