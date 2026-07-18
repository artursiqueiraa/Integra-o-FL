namespace CentralHub.SDK.Jfl.Protocol;

/// <summary>Monta pacotes 0x7B prontos para transmissao, calculando QDE e o checksum.</summary>
public static class PacketBuilder
{
    /// <summary>
    /// Monta um pacote 0x7B: [0x7B][QDE][SEQ][CMD][DADOS...][K].
    /// </summary>
    /// <param name="seq">Byte de sequencia (0x01 a 0xFF) — na resposta a um comando, deve ecoar o SEQ recebido.</param>
    /// <param name="cmd">Byte de comando.</param>
    /// <param name="dados">Payload do comando (pode ser vazio, ex.: keep-alive de pedido).</param>
    public static byte[] Build(byte seq, byte cmd, ReadOnlySpan<byte> dados)
    {
        var tamanho = JflProtocol.MinPacketLength + dados.Length;
        if (tamanho > JflProtocol.MaxPacketLength)
        {
            throw new JflProtocolException(
                $"Pacote 0x7B excede o tamanho maximo de {JflProtocol.MaxPacketLength} bytes (tamanho calculado: {tamanho}).");
        }

        var buffer = new byte[tamanho];
        buffer[0] = JflProtocol.Header0x7B;
        buffer[1] = (byte)tamanho;
        buffer[2] = seq;
        buffer[3] = cmd;
        dados.CopyTo(buffer.AsSpan(4));

        buffer[tamanho - 1] = ChecksumCalculator.Calculate(buffer.AsSpan(0, tamanho - 1));

        return buffer;
    }
}
