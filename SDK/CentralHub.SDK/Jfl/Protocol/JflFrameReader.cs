namespace CentralHub.SDK.Jfl.Protocol;

/// <summary>
/// Le pacotes 0x7B completos a partir de um <see cref="Stream"/> (tipicamente um
/// <see cref="System.Net.Sockets.NetworkStream"/>), lidando com fragmentacao TCP:
/// um <c>Read</c> pode trazer menos de um pacote (nesse caso continua lendo) ou
/// mais de um pacote de uma vez (nesse caso os pacotes extras ficam no buffer
/// interno para a proxima chamada de <see cref="ReadPacketAsync"/>).
/// </summary>
public sealed class JflFrameReader
{
    private readonly Stream _stream;
    private byte[] _buffer = new byte[512];
    private int _length;

    public JflFrameReader(Stream stream)
    {
        _stream = stream;
    }

    /// <summary>
    /// Le o proximo pacote do stream. Retorna <c>null</c> quando o peer fechou a conexao
    /// (fim de stream) antes de completar um pacote.
    /// </summary>
    public async Task<JflPacket?> ReadPacketAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var resultado = PacketParser.TryParse(_buffer.AsSpan(0, _length));

            switch (resultado.Status)
            {
                case JflParseStatus.Success:
                    Consumir(resultado.BytesConsumed);
                    return resultado.Packet;

                case JflParseStatus.InvalidHeader:
                case JflParseStatus.ChecksumMismatch:
                    // Descarta os bytes invalidos/corrompidos e tenta ressincronizar
                    // com o restante do buffer antes de pedir mais dados do socket.
                    Consumir(Math.Max(resultado.BytesConsumed, 1));
                    continue;

                case JflParseStatus.NeedMoreData:
                    if (!await PreencherAsync(cancellationToken).ConfigureAwait(false))
                    {
                        return null;
                    }

                    continue;

                default:
                    throw new InvalidOperationException($"Status de parse nao tratado: {resultado.Status}");
            }
        }
    }

    private void Consumir(int quantidade)
    {
        if (quantidade <= 0)
        {
            return;
        }

        var restante = _length - quantidade;
        if (restante > 0)
        {
            Array.Copy(_buffer, quantidade, _buffer, 0, restante);
        }

        _length = restante;
    }

    private async Task<bool> PreencherAsync(CancellationToken cancellationToken)
    {
        if (_length == _buffer.Length)
        {
            Array.Resize(ref _buffer, _buffer.Length * 2);
        }

        var lidos = await _stream.ReadAsync(_buffer.AsMemory(_length, _buffer.Length - _length), cancellationToken)
            .ConfigureAwait(false);

        if (lidos == 0)
        {
            return false;
        }

        _length += lidos;
        return true;
    }
}
