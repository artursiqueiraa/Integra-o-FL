using CentralHub.SDK.Jfl.Protocol;
using CentralHub.SDK.Tests.TestUtilities;

namespace CentralHub.SDK.Tests.Protocol;

public class JflFrameReaderTests
{
    [Fact]
    public async Task ReadPacketAsync_deve_montar_o_pacote_mesmo_recebendo_1_byte_por_vez()
    {
        var pacoteOriginal = PacketBuilder.Build(seq: 0x18, cmd: 0x40, dados: ReadOnlySpan<byte>.Empty);
        await using var stream = new TrickleStream(pacoteOriginal, bytesPorLeitura: 1);
        var reader = new JflFrameReader(stream);

        var pacote = await reader.ReadPacketAsync(CancellationToken.None);

        Assert.NotNull(pacote);
        Assert.Equal(0x18, pacote!.Seq);
        Assert.Equal(0x40, pacote.Cmd);
    }

    [Fact]
    public async Task ReadPacketAsync_deve_ler_dois_pacotes_sequenciais_do_mesmo_stream()
    {
        var pacote1 = PacketBuilder.Build(0x01, 0x21, [0xAA]);
        var pacote2 = PacketBuilder.Build(0x02, 0x40, ReadOnlySpan<byte>.Empty);
        var bytes = pacote1.Concat(pacote2).ToArray();

        await using var stream = new TrickleStream(bytes, bytesPorLeitura: 3);
        var reader = new JflFrameReader(stream);

        var primeiro = await reader.ReadPacketAsync(CancellationToken.None);
        var segundo = await reader.ReadPacketAsync(CancellationToken.None);

        Assert.Equal(0x01, primeiro!.Seq);
        Assert.Equal(0x21, primeiro.Cmd);
        Assert.Equal(0x02, segundo!.Seq);
        Assert.Equal(0x40, segundo.Cmd);
    }

    [Fact]
    public async Task ReadPacketAsync_deve_ressincronizar_apos_lixo_antes_de_um_pacote_valido()
    {
        var lixo = new byte[] { 0x00, 0x11, 0x22 };
        var pacoteValido = PacketBuilder.Build(0x05, 0x40, ReadOnlySpan<byte>.Empty);
        var bytes = lixo.Concat(pacoteValido).ToArray();

        await using var stream = new TrickleStream(bytes, bytesPorLeitura: 2);
        var reader = new JflFrameReader(stream);

        var pacote = await reader.ReadPacketAsync(CancellationToken.None);

        Assert.NotNull(pacote);
        Assert.Equal(0x05, pacote!.Seq);
    }

    [Fact]
    public async Task ReadPacketAsync_deve_retornar_null_quando_o_stream_acaba_sem_completar_um_pacote()
    {
        byte[] bytesIncompletos = [0x7B, 0x06, 0x01]; // QDE diz 6, so vieram 3

        await using var stream = new TrickleStream(bytesIncompletos, bytesPorLeitura: 1);
        var reader = new JflFrameReader(stream);

        var pacote = await reader.ReadPacketAsync(CancellationToken.None);

        Assert.Null(pacote);
    }
}
