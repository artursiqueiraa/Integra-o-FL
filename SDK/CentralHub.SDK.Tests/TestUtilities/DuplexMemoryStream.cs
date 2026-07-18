namespace CentralHub.SDK.Tests.TestUtilities;

/// <summary>
/// Stream de teste que simula o comportamento full-duplex de um NetworkStream:
/// le de um buffer de entrada pre-preenchido (o que o "equipamento" enviaria) e
/// escreve num buffer de saida separado (o que o servidor respondeu), permitindo
/// testar handlers/sessoes sem sockets reais.
/// </summary>
public sealed class DuplexMemoryStream : Stream
{
    private readonly MemoryStream _entrada;

    public MemoryStream Saida { get; } = new();

    public DuplexMemoryStream(byte[] bytesDeEntrada)
    {
        _entrada = new MemoryStream(bytesDeEntrada);
    }

    public byte[] SaidaComoArray() => Saida.ToArray();

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => _entrada.Read(buffer, offset, count);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _entrada.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _entrada.ReadAsync(buffer, cancellationToken);

    public override void Write(byte[] buffer, int offset, int count) => Saida.Write(buffer, offset, count);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Saida.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        Saida.WriteAsync(buffer, cancellationToken);

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
