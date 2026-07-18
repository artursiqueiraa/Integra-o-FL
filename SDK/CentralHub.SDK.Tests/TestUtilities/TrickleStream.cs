namespace CentralHub.SDK.Tests.TestUtilities;

/// <summary>
/// Stream de leitura que devolve no maximo <see cref="BytesPorLeitura"/> bytes por
/// chamada, mesmo que o chamador peca mais — simula a fragmentacao real de um
/// socket TCP, onde um unico pacote pode chegar espalhado por varias leituras.
/// </summary>
public sealed class TrickleStream : Stream
{
    private readonly byte[] _dados;
    private readonly int _bytesPorLeitura;
    private int _posicao;

    public TrickleStream(byte[] dados, int bytesPorLeitura = 1)
    {
        _dados = dados;
        _bytesPorLeitura = bytesPorLeitura;
    }

    public override bool CanRead => true;
    public override bool CanWrite => false;
    public override bool CanSeek => false;
    public override long Length => _dados.Length;

    public override long Position
    {
        get => _posicao;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_posicao >= _dados.Length)
        {
            return 0;
        }

        var quantidade = Math.Min(Math.Min(count, _bytesPorLeitura), _dados.Length - _posicao);
        Array.Copy(_dados, _posicao, buffer, offset, quantidade);
        _posicao += quantidade;
        return quantidade;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_posicao >= _dados.Length)
        {
            return ValueTask.FromResult(0);
        }

        var quantidade = Math.Min(Math.Min(buffer.Length, _bytesPorLeitura), _dados.Length - _posicao);
        _dados.AsSpan(_posicao, quantidade).CopyTo(buffer.Span);
        _posicao += quantidade;
        return ValueTask.FromResult(quantidade);
    }

    public override void Flush() => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
