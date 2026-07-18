using CentralHub.SDK.Jfl.Messages;
using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server.Handlers;

/// <summary>
/// Trata o comando de conexao (3.1): decodifica NS/IMEI/MAC/modelo/versao, decide se
/// a central esta liberada via <see cref="ICentralAuthorizationProvider"/>, responde
/// RESULT+KEEP e, se liberada, registra a sessao no <see cref="SessionManager"/>.
/// </summary>
public sealed class ConnectionCommandHandler : IJflCommandHandler
{
    private readonly SessionManager _sessionManager;
    private readonly ICentralAuthorizationProvider _authorizationProvider;
    private readonly JflServerOptions _options;
    private readonly ILogger<ConnectionCommandHandler> _logger;

    public ConnectionCommandHandler(
        SessionManager sessionManager,
        ICentralAuthorizationProvider authorizationProvider,
        JflServerOptions options,
        ILogger<ConnectionCommandHandler> logger)
    {
        _sessionManager = sessionManager;
        _authorizationProvider = authorizationProvider;
        _options = options;
        _logger = logger;
    }

    public bool CanHandle(byte cmd) => cmd == (byte)JflCommand.Conexao || cmd == (byte)JflCommand.ConexaoModulo;

    public async Task HandleAsync(JflSession session, JflPacket packet, CancellationToken cancellationToken)
    {
        ConnectionRequest requisicao;
        try
        {
            requisicao = ConnectionRequest.Parse(packet.Dados);
        }
        catch (JflProtocolException ex)
        {
            _logger.LogWarning(ex, "Comando de conexao invalido recebido de {RemoteEndPoint}", session.RemoteEndPoint);
            return;
        }

        session.NumeroSerie = requisicao.NumeroSerie;
        session.Imei = requisicao.Imei;
        session.Mac = requisicao.Mac;
        session.Modelo = requisicao.Modelo;
        session.VersaoFirmware = requisicao.Versao;

        _logger.LogInformation(
            "Conexao recebida (cmd 0x{Cmd:X2}) de {RemoteEndPoint}: NS={NumeroSerie} Modelo={Modelo} Versao={Versao} " +
            "IMEI={Imei} MAC={Mac} Via={Via} Sinal/Status={StatusBytes} bytes",
            packet.Cmd,
            session.RemoteEndPoint,
            requisicao.NumeroSerie,
            requisicao.Modelo.ToNomeAmigavel(),
            requisicao.Versao,
            requisicao.Imei ?? "(vazio)",
            requisicao.Mac ?? "(vazio)",
            requisicao.Via == 0x01 ? "Ethernet" : "GPRS",
            requisicao.StatusPayload.Length);

        var liberada = await _authorizationProvider.EstaLiberadaAsync(requisicao.NumeroSerie, cancellationToken)
            .ConfigureAwait(false);

        var resposta = new ConnectionResponse
        {
            Resultado = liberada ? ConnectionResult.Liberado : ConnectionResult.Bloqueado,
            IntervaloKeepAliveMinutos = _options.IntervaloKeepAliveMinutos,
        };

        await session.ReplyAsync(packet, packet.Cmd, resposta.ToDados(), cancellationToken).ConfigureAwait(false);

        if (!liberada)
        {
            _logger.LogWarning(
                "Central {NumeroSerie} bloqueada (nao autorizada); aguardando o equipamento encerrar a conexao",
                requisicao.NumeroSerie);
            return;
        }

        session.State = JflSessionState.Ativa;
        _sessionManager.Registrar(session);

        _logger.LogInformation(
            "Central {NumeroSerie} ({Modelo}) autenticada; keep-alive definido para {Minutos} min",
            requisicao.NumeroSerie, requisicao.Modelo.ToNomeAmigavel(), _options.IntervaloKeepAliveMinutos);
    }
}
