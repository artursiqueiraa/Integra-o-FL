using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using CentralHub.SDK.Jfl.Protocol;

namespace CentralHub.Simulator;

/// <summary>
/// Simula uma central JFL Active 100 Bus do lado cliente: abre o socket, faz o handshake
/// (0x21), mantém o ciclo de keep-alive (0x40), responde aos comandos de superusuário
/// (Status/Armar/Desarmar/PGM/Zonas/Data-Hora) usando o <see cref="EstadoCentralSimulada"/>
/// injetável, e pode disparar Eventos (0x24) sob demanda — sem precisar de hardware físico.
/// Reaproveita só <see cref="PacketBuilder"/>/<see cref="JflFrameReader"/>/
/// <see cref="ChecksumCalculator"/>/<see cref="JflCommand"/> do SDK (utilitários de framing
/// genéricos); nunca <c>SessionManager</c>/<c>JflSession</c>, que são conceitos do lado
/// servidor.
/// </summary>
public sealed class SimuladorActive100Bus : IAsyncDisposable
{
    private static readonly TimeSpan TimeoutPadraoResposta = TimeSpan.FromSeconds(10);

    private TcpClient? _client;
    private NetworkStream? _stream;
    private JflFrameReader? _reader;
    private CancellationTokenSource? _ctsLoop;
    private Task? _loopRecepcao;
    private Task? _loopKeepAlive;
    private byte _seqAtual;
    private byte _intervaloKeepAliveMinutos = 1;
    private bool _checksumInvalidoNoProximoEnvio;
    private bool _pacoteQuebradoNoProximoEnvio;
    private bool _ignorarProximoComandoSuperusuario;

    private readonly ConcurrentDictionary<byte, TaskCompletionSource<JflPacket>> _respostasPendentes = new();

    public string NumeroSerie { get; }

    public EstadoCentralSimulada Estado { get; }

    public bool Conectado => _client?.Connected == true;

    /// <summary>Disparado sempre que um comando de superusuário (Status/Armar/PGM/Zonas/Data-Hora) é recebido e respondido automaticamente.</summary>
    public event Action<JflPacket>? ComandoRecebido;

    public SimuladorActive100Bus(string numeroSerie, EstadoCentralSimulada? estadoInicial = null)
    {
        if (numeroSerie.Length != 10)
        {
            throw new ArgumentException("Número de série deve ter exatamente 10 caracteres.", nameof(numeroSerie));
        }

        NumeroSerie = numeroSerie;
        Estado = estadoInicial ?? new EstadoCentralSimulada();
    }

    /// <summary>Conecta e realiza o handshake (0x21). Se liberado, inicia os laços de recepção e keep-alive.</summary>
    public async Task<(bool Liberado, byte KeepAliveMinutos)> ConectarAsync(string host, int porta, CancellationToken cancellationToken)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(host, porta, cancellationToken).ConfigureAwait(false);
        _stream = _client.GetStream();
        _reader = new JflFrameReader(_stream);

        _ctsLoop = new CancellationTokenSource();
        _loopRecepcao = LoopRecepcaoAsync(_ctsLoop.Token);

        var resposta = await EnviarEAguardarAsync((byte)JflCommand.Conexao, MontarDadosConexao(), cancellationToken)
            .ConfigureAwait(false);

        var liberado = resposta.Dados[0] == 0x01;
        var keep = resposta.Dados[1];
        _intervaloKeepAliveMinutos = keep is >= 1 and <= 20 ? keep : (byte)1;

        if (liberado)
        {
            _loopKeepAlive = LoopKeepAliveAsync(_ctsLoop.Token);
        }

        return (liberado, _intervaloKeepAliveMinutos);
    }

    /// <summary>Dispara um Evento (0x24) — payload base do protocolo 0x7B (sem os campos exclusivos do 7A).</summary>
    public async Task<bool> DispararEventoAsync(
        string codigoContactId, int particao, string usuarioOuZona, uint contador, byte spart, bool comProblema,
        CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        if (codigoContactId.Length != 4 || usuarioOuZona.Length != 3)
        {
            throw new ArgumentException("Código Contact ID deve ter 4 dígitos e usuário/zona 3 dígitos.");
        }

        var dados = new byte[19];
        var offset = 0;
        EscreverAscii(dados, ref offset, "0000", 4); // CONTA (sem conta configurada no simulador)
        EscreverAscii(dados, ref offset, codigoContactId, 4);
        EscreverAscii(dados, ref offset, particao.ToString("D2"), 2);
        EscreverAscii(dados, ref offset, usuarioOuZona, 3);
        var contadorBytes = BitConverter.GetBytes(contador);
        if (BitConverter.IsLittleEndian) Array.Reverse(contadorBytes);
        Array.Copy(contadorBytes, 0, dados, offset, 4);
        offset += 4;
        dados[offset++] = spart;
        dados[offset] = (byte)(comProblema ? 0x01 : 0x00);

        var resposta = await EnviarEAguardarAsync((byte)JflCommand.Evento, dados, cancellationToken, timeout).ConfigureAwait(false);
        return resposta.Dados.Length > 0 && resposta.Dados[0] == 0x01;
    }

    public void SimularChecksumInvalido() => _checksumInvalidoNoProximoEnvio = true;

    public void SimularPacoteQuebrado() => _pacoteQuebradoNoProximoEnvio = true;

    public void SimularDesconexao() => _client?.Close();

    /// <summary>O próximo comando de superusuário recebido não será respondido — simula uma central que não responde a tempo.</summary>
    public void SimularTimeout() => _ignorarProximoComandoSuperusuario = true;

    /// <summary>Envia um keep-alive imediatamente, fora do laço automático — útil para testes de carga.</summary>
    public Task EnviarKeepAliveAsync(CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        EnviarEAguardarAsync((byte)JflCommand.KeepAlive, [], cancellationToken, timeout);

    /// <summary>Reabre a conexão após <see cref="SimularDesconexao"/>, com o mesmo número de série.</summary>
    public Task<(bool Liberado, byte KeepAliveMinutos)> ReconectarAsync(string host, int porta, CancellationToken cancellationToken) =>
        ConectarAsync(host, porta, cancellationToken);

    private async Task<JflPacket> EnviarEAguardarAsync(byte cmd, byte[] dados, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        var seq = ProximoSeq();
        var tcs = new TaskCompletionSource<JflPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
        _respostasPendentes[seq] = tcs;

        await EnviarAsync(seq, cmd, dados, cancellationToken).ConfigureAwait(false);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout ?? TimeoutPadraoResposta);
        await using (cts.Token.Register(() => tcs.TrySetCanceled()).ConfigureAwait(false))
        {
            try
            {
                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                _respostasPendentes.TryRemove(seq, out _);
            }
        }
    }

    private async Task EnviarAsync(byte seq, byte cmd, byte[] dados, CancellationToken cancellationToken)
    {
        var pacote = PacketBuilder.Build(seq, cmd, dados);

        if (_checksumInvalidoNoProximoEnvio)
        {
            pacote[^1] ^= 0xFF; // corrompe o checksum de proposito
            _checksumInvalidoNoProximoEnvio = false;
        }

        if (_pacoteQuebradoNoProximoEnvio)
        {
            pacote = pacote[..Math.Max(1, pacote.Length / 2)]; // envia so metade do pacote
            _pacoteQuebradoNoProximoEnvio = false;
        }

        await _stream!.WriteAsync(pacote, cancellationToken).ConfigureAwait(false);
    }

    private byte ProximoSeq()
    {
        _seqAtual = _seqAtual == 0xFF ? (byte)0x01 : (byte)(_seqAtual + 1);
        return _seqAtual;
    }

    private async Task LoopRecepcaoAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var pacote = await _reader!.ReadPacketAsync(cancellationToken).ConfigureAwait(false);
                if (pacote is null)
                {
                    return; // conexao encerrada pelo servidor
                }

                if (_respostasPendentes.TryRemove(pacote.Seq, out var tcs))
                {
                    tcs.TrySetResult(pacote);
                    continue;
                }

                if (EhComandoDeSuperusuario(pacote.Cmd))
                {
                    await ResponderComandoSuperusuarioAsync(pacote, cancellationToken).ConfigureAwait(false);
                    ComandoRecebido?.Invoke(pacote);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // esperado ao encerrar o simulador.
        }
        catch (IOException)
        {
            // conexao encerrada abruptamente (ex.: apos SimularDesconexao()).
        }
    }

    private static bool EhComandoDeSuperusuario(byte cmd) => cmd is
        (byte)JflCommand.Status or (byte)JflCommand.Armar or (byte)JflCommand.Desarmar or
        (byte)JflCommand.AcionarPgm or (byte)JflCommand.DesacionarPgm or (byte)JflCommand.InibirZonas or
        (byte)JflCommand.ArmarStay or (byte)JflCommand.ArmarAway or (byte)JflCommand.AtualizarDataHora;

    private async Task ResponderComandoSuperusuarioAsync(JflPacket pacote, CancellationToken cancellationToken)
    {
        if (_ignorarProximoComandoSuperusuario)
        {
            _ignorarProximoComandoSuperusuario = false;
            return; // simula uma central que nao respondeu a tempo (SimularTimeout)
        }

        AplicarComandoAoEstado((JflCommand)pacote.Cmd, pacote.Dados);
        var resposta = Estado.MontarRespostaTelaMonitorar();
        await EnviarAsync(pacote.Seq, pacote.Cmd, resposta, cancellationToken).ConfigureAwait(false);
    }

    private void AplicarComandoAoEstado(JflCommand cmd, byte[] dados)
    {
        switch (cmd)
        {
            case JflCommand.Armar or JflCommand.ArmarAway when dados.Length == 1 && dados[0] is >= 1 and <= 16:
                Estado.Particoes[dados[0] - 1] = 0x02; // Armada
                break;
            case JflCommand.ArmarStay when dados.Length == 1 && dados[0] is >= 1 and <= 16:
                Estado.Particoes[dados[0] - 1] = 0x03; // ArmadaStay
                break;
            case JflCommand.Desarmar when dados.Length == 1 && dados[0] is >= 1 and <= 16:
                Estado.Particoes[dados[0] - 1] = 0x01; // Desarmada
                break;
            case JflCommand.Armar or JflCommand.ArmarAway or JflCommand.Desarmar when dados.Length == 1 && dados[0] == 99:
                Estado.Eletrificador = cmd == JflCommand.Desarmar ? (byte)0x01 : (byte)0x02;
                break;
            case JflCommand.AcionarPgm when dados.Length == 1 && dados[0] is >= 1 and <= 16:
                Estado.PgmsAcionadas[dados[0] - 1] = true;
                break;
            case JflCommand.DesacionarPgm when dados.Length == 1 && dados[0] is >= 1 and <= 16:
                Estado.PgmsAcionadas[dados[0] - 1] = false;
                break;
            case JflCommand.InibirZonas when dados.Length == 13:
                AplicarBitmapZonas(dados);
                break;
            case JflCommand.AtualizarDataHora when dados.Length == 6:
                Estado.DataHora = new DateTime(
                    2000 + ParseBcd(dados[5]), ParseBcd(dados[4]), ParseBcd(dados[3]),
                    ParseBcd(dados[0]), ParseBcd(dados[1]), ParseBcd(dados[2]));
                break;
        }
    }

    /// <summary>
    /// Convenção MSB-first por byte confirmada contra capturas reais do manual (bit 7 = zona
    /// menor do byte) — diferente do bitmap P-INIB, que é LSB-first. Semântica é
    /// "substituir o conjunto inteiro", não somar.
    /// </summary>
    private void AplicarBitmapZonas(byte[] bitmap)
    {
        for (var numero = 1; numero <= EstadoCentralSimulada.QuantidadeZonas; numero++)
        {
            var indiceByte = (numero - 1) / 8;
            if (indiceByte >= bitmap.Length)
            {
                continue;
            }

            var bit = 7 - ((numero - 1) % 8);
            var inibida = (bitmap[indiceByte] & (1 << bit)) != 0;

            var estadoAtual = Estado.Zonas[numero - 1];
            if (estadoAtual == 0x00 && !inibida)
            {
                continue; // zona desabilitada continua desabilitada
            }

            Estado.Zonas[numero - 1] = inibida ? (byte)0x01 : (byte)0x07; // Inibida ou Aberta (default "normal")
        }
    }

    private static int ParseBcd(byte valor) => ((valor >> 4) * 10) + (valor & 0x0F);

    private byte[] MontarDadosConexao()
    {
        var dados = new byte[45];
        var offset = 0;
        EscreverAscii(dados, ref offset, NumeroSerie, 10);
        EscreverPreenchido(dados, ref offset, 15, 0xFF); // IMEI vazio
        EscreverPreenchido(dados, ref offset, 12, 0xFF); // MAC vazio
        dados[offset++] = (byte)JflModel.Active100Bus;
        EscreverAscii(dados, ref offset, "650", 3); // versão 6.5.0 (firmware documentado do projeto: 6.5)
        dados[offset++] = 0x01; // IP
        dados[offset++] = 0x03; // SIMCARD: não existe (Ethernet)
        dados[offset++] = 0x01; // VIA: Ethernet
        dados[offset] = 0x06; // OPE: não existe
        return dados;
    }

    private static void EscreverAscii(byte[] destino, ref int offset, string valor, int tamanho)
    {
        if (valor.Length != tamanho)
        {
            throw new ArgumentException($"Valor '{valor}' deveria ter {tamanho} caracteres.", nameof(valor));
        }

        Encoding.ASCII.GetBytes(valor).CopyTo(destino, offset);
        offset += tamanho;
    }

    private static void EscreverPreenchido(byte[] destino, ref int offset, int tamanho, byte valor)
    {
        Array.Fill(destino, valor, offset, tamanho);
        offset += tamanho;
    }

    public async ValueTask DisposeAsync()
    {
        _ctsLoop?.Cancel();

        if (_loopRecepcao is not null)
        {
            try { await _loopRecepcao.ConfigureAwait(false); } catch { /* esperado */ }
        }

        if (_loopKeepAlive is not null)
        {
            try { await _loopKeepAlive.ConfigureAwait(false); } catch { /* esperado */ }
        }

        _client?.Dispose();
        _ctsLoop?.Dispose();
    }

    private async Task LoopKeepAliveAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(_intervaloKeepAliveMinutos), cancellationToken).ConfigureAwait(false);
                var resposta = await EnviarEAguardarAsync((byte)JflCommand.KeepAlive, [], cancellationToken).ConfigureAwait(false);
                if (resposta.Dados.Length == 1)
                {
                    var keep = resposta.Dados[0];
                    _intervaloKeepAliveMinutos = keep is >= 1 and <= 20 ? keep : (byte)1;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // esperado ao encerrar o simulador.
        }
    }
}
