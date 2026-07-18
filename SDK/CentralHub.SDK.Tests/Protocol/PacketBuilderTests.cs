using CentralHub.SDK.Jfl.Protocol;

namespace CentralHub.SDK.Tests.Protocol;

public class PacketBuilderTests
{
    [Fact]
    public void Build_comando_de_keep_alive_deve_bater_byte_a_byte_com_captura_real()
    {
        // TX[5]=7B 05 18 40 26 (equipamento pedindo keep-alive, seq 0x18).
        var pacote = PacketBuilder.Build(seq: 0x18, cmd: 0x40, dados: ReadOnlySpan<byte>.Empty);

        Assert.Equal(new byte[] { 0x7B, 0x05, 0x18, 0x40, 0x26 }, pacote);
    }

    [Fact]
    public void Build_resposta_de_keep_alive_com_1_minuto_deve_bater_com_captura_real()
    {
        // RX[6]=7B 06 18 40 00 25 (resposta com KEEP=0x00, ou seja, 1 minuto).
        var pacote = PacketBuilder.Build(seq: 0x18, cmd: 0x40, dados: [0x00]);

        Assert.Equal(new byte[] { 0x7B, 0x06, 0x18, 0x40, 0x00, 0x25 }, pacote);
    }

    [Fact]
    public void Build_deve_calcular_QDE_como_o_tamanho_total_do_pacote()
    {
        var pacote = PacketBuilder.Build(seq: 0x01, cmd: 0x21, dados: [0xAA, 0xBB, 0xCC]);

        // CAB+QDE+SEQ+CMD+3 dados+K = 8.
        Assert.Equal(8, pacote.Length);
        Assert.Equal(8, pacote[1]);
    }

    [Fact]
    public void Build_pacote_resultante_deve_sempre_passar_no_proprio_checksum()
    {
        var pacote = PacketBuilder.Build(seq: 0x42, cmd: 0x24, dados: [0x01, 0x02, 0x03, 0x04, 0x05]);

        Assert.True(ChecksumCalculator.IsValid(pacote));
    }

    [Fact]
    public void Build_deve_lancar_quando_pacote_excede_255_bytes()
    {
        var dadosGrandes = new byte[252]; // 252 + 5 (framing) = 257 > 255

        Assert.Throws<JflProtocolException>(() => PacketBuilder.Build(0x01, 0x21, dadosGrandes));
    }
}
