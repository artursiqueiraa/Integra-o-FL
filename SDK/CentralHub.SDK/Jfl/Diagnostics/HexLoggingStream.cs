using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Diagnostics;

/// <summary>
/// Decorator transparente de <see cref="Stream"/>: repassa todo <c>Read</c>/<c>Write</c> sem
/// alterar nenhum byte, opcionalmente logando cada chamada em hex+ASCII. Comportamento
/// idêntico ao stream de baixo, com ou sem logging habilitado — é I/O puro, nenhuma lógica de
/// protocolo. Desligado por padrão (opt-in via <c>Jfl:LogHexAtivado</c> em appsettings.json).
/// </summary>
public sealed class HexLoggingStream : Stream
{
    private readonly Stream _interno;
    private readonly ILogger _logger;
    private readonly string _identificadorConexao;

    public HexLoggingStream(Stream interno, ILogger logger, string identificadorConexao)
    {
        _interno = interno;
        _logger = logger;
        _identificadorConexao = identificadorConexao;
    }

    public override bool CanRead => _interno.CanRead;
    public override bool CanWrite => _interno.CanWrite;
    public override bool CanSeek => _interno.CanSeek;
    public override long Length => _interno.Length;

    public override long Position
    {
        get => _interno.Position;
        set => _interno.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var lidos = _interno.Read(buffer, offset, count);
        LogarSeHabilitado("RX", buffer.AsSpan(offset, lidos));
        return lidos;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var lidos = await _interno.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        LogarSeHabilitado("RX", buffer.Span[..lidos]);
        return lidos;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Write(byte[] buffer, int offset, int count)
    {
        LogarSeHabilitado("TX", buffer.AsSpan(offset, count));
        _interno.Write(buffer, offset, count);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        LogarSeHabilitado("TX", buffer.Span);
        await _interno.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private void LogarSeHabilitado(string direcao, ReadOnlySpan<byte> dados)
    {
        if (dados.Length == 0 || !_logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        var hex = Convert.ToHexString(dados);
        var ascii = new string(dados.ToArray().Select(b => b is >= 32 and < 127 ? (char)b : '.').ToArray());
        _logger.LogDebug(
            "{Direcao} [{Conexao}] {Timestamp:HH:mm:ss.fff} Tamanho={Tamanho} HEX={Hex} ASCII={Ascii}",
            direcao, _identificadorConexao, DateTime.Now, dados.Length, hex, ascii);
    }

    public override void Flush() => _interno.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => _interno.FlushAsync(cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => _interno.Seek(offset, origin);

    public override void SetLength(long value) => _interno.SetLength(value);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _interno.Dispose();
        }

        base.Dispose(disposing);
    }
}
