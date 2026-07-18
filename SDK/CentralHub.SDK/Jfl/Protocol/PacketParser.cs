namespace CentralHub.SDK.Jfl.Protocol;

public enum JflParseStatus
{
    /// <summary>O buffer ainda nao contem um pacote completo; aguardar mais bytes do socket.</summary>
    NeedMoreData,

    /// <summary>Pacote decodificado e com checksum valido.</summary>
    Success,

    /// <summary>O primeiro byte nao e 0x7B, ou o QDE declarado e menor que o minimo possivel.</summary>
    InvalidHeader,

    /// <summary>O pacote tem o tamanho declarado certo, mas o XOR de checagem nao fechou em zero.</summary>
    ChecksumMismatch,
}

/// <summary>
/// Resultado de uma tentativa de parse. <see cref="BytesConsumed"/> indica quantos
/// bytes do inicio do buffer o chamador deve descartar antes de tentar novamente
/// (0 quando e preciso ler mais dados do socket antes de tentar de novo).
/// </summary>
public readonly struct JflParseResult
{
    public JflParseStatus Status { get; }

    public JflPacket? Packet { get; }

    public int BytesConsumed { get; }

    private JflParseResult(JflParseStatus status, JflPacket? packet, int bytesConsumed)
    {
        Status = status;
        Packet = packet;
        BytesConsumed = bytesConsumed;
    }

    public static JflParseResult NeedMoreData() => new(JflParseStatus.NeedMoreData, null, 0);

    public static JflParseResult InvalidHeader(int bytesToSkip) => new(JflParseStatus.InvalidHeader, null, bytesToSkip);

    public static JflParseResult ChecksumMismatch(int bytesConsumed) => new(JflParseStatus.ChecksumMismatch, null, bytesConsumed);

    public static JflParseResult Success(JflPacket packet, int bytesConsumed) => new(JflParseStatus.Success, packet, bytesConsumed);
}

/// <summary>
/// Parser puro (sem I/O) de pacotes 0x7B. Opera sobre um buffer que pode conter um
/// pacote parcial, um pacote completo, ou varios pacotes concatenados — o chamador
/// (tipicamente <see cref="JflFrameReader"/>) e responsavel por acumular bytes do
/// socket e descartar <see cref="JflParseResult.BytesConsumed"/> a cada chamada.
/// </summary>
public static class PacketParser
{
    public static JflParseResult TryParse(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 1)
        {
            return JflParseResult.NeedMoreData();
        }

        if (buffer[0] != JflProtocol.Header0x7B)
        {
            return JflParseResult.InvalidHeader(1);
        }

        if (buffer.Length < 2)
        {
            return JflParseResult.NeedMoreData();
        }

        int tamanho = buffer[1];
        if (tamanho < JflProtocol.MinPacketLength)
        {
            // QDE nao pode ser menor que CAB+QDE+SEQ+CMD+K; cabecalho corrompido ou
            // fora de sincronia — descarta so o byte de cabecalho e tenta ressincronizar.
            return JflParseResult.InvalidHeader(1);
        }

        if (buffer.Length < tamanho)
        {
            return JflParseResult.NeedMoreData();
        }

        var pacoteCompleto = buffer[..tamanho];
        if (!ChecksumCalculator.IsValid(pacoteCompleto))
        {
            return JflParseResult.ChecksumMismatch(tamanho);
        }

        var seq = buffer[2];
        var cmd = buffer[3];
        var dados = buffer.Slice(4, tamanho - JflProtocol.MinPacketLength).ToArray();

        var pacote = new JflPacket { Seq = seq, Cmd = cmd, Dados = dados };
        return JflParseResult.Success(pacote, tamanho);
    }
}
