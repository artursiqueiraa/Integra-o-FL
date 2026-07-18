using CentralHub.SDK.Jfl.Protocol;

namespace CentralHub.SDK.Tests.Protocol;

public class PacketParserTests
{
    [Fact]
    public void TryParse_buffer_vazio_deve_pedir_mais_dados()
    {
        var resultado = PacketParser.TryParse(ReadOnlySpan<byte>.Empty);

        Assert.Equal(JflParseStatus.NeedMoreData, resultado.Status);
    }

    [Fact]
    public void TryParse_buffer_com_so_o_cabecalho_deve_pedir_mais_dados()
    {
        byte[] buffer = [0x7B];

        var resultado = PacketParser.TryParse(buffer);

        Assert.Equal(JflParseStatus.NeedMoreData, resultado.Status);
    }

    [Fact]
    public void TryParse_pacote_declarado_mas_incompleto_deve_pedir_mais_dados()
    {
        // QDE diz 5 bytes, mas so vieram 3 ate agora.
        byte[] buffer = [0x7B, 0x05, 0x18];

        var resultado = PacketParser.TryParse(buffer);

        Assert.Equal(JflParseStatus.NeedMoreData, resultado.Status);
    }

    [Fact]
    public void TryParse_pacote_completo_e_valido_deve_decodificar_corretamente()
    {
        // Keep-alive real: 7B 05 18 40 26.
        byte[] buffer = [0x7B, 0x05, 0x18, 0x40, 0x26];

        var resultado = PacketParser.TryParse(buffer);

        Assert.Equal(JflParseStatus.Success, resultado.Status);
        Assert.Equal(5, resultado.BytesConsumed);
        Assert.NotNull(resultado.Packet);
        Assert.Equal(0x18, resultado.Packet!.Seq);
        Assert.Equal(0x40, resultado.Packet.Cmd);
        Assert.Empty(resultado.Packet.Dados);
    }

    [Fact]
    public void TryParse_deve_extrair_os_bytes_de_dados_corretamente()
    {
        var pacoteMontado = PacketBuilder.Build(seq: 0x10, cmd: 0x21, dados: [0xAA, 0xBB, 0xCC, 0xDD]);

        var resultado = PacketParser.TryParse(pacoteMontado);

        Assert.Equal(JflParseStatus.Success, resultado.Status);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, resultado.Packet!.Dados);
    }

    [Fact]
    public void TryParse_cabecalho_invalido_deve_sinalizar_para_descartar_1_byte()
    {
        byte[] buffer = [0xFF, 0x05, 0x18, 0x40, 0x26];

        var resultado = PacketParser.TryParse(buffer);

        Assert.Equal(JflParseStatus.InvalidHeader, resultado.Status);
        Assert.Equal(1, resultado.BytesConsumed);
    }

    [Fact]
    public void TryParse_checksum_invalido_deve_sinalizar_para_descartar_o_pacote_inteiro()
    {
        byte[] buffer = [0x7B, 0x05, 0x18, 0x40, 0x00]; // checksum errado (deveria ser 0x26)

        var resultado = PacketParser.TryParse(buffer);

        Assert.Equal(JflParseStatus.ChecksumMismatch, resultado.Status);
        Assert.Equal(5, resultado.BytesConsumed);
    }

    [Fact]
    public void TryParse_deve_decodificar_apenas_o_primeiro_pacote_quando_ha_varios_concatenados()
    {
        var pacote1 = PacketBuilder.Build(0x01, 0x40, ReadOnlySpan<byte>.Empty);
        var pacote2 = PacketBuilder.Build(0x02, 0x21, [0x01, 0x02]);
        var buffer = pacote1.Concat(pacote2).ToArray();

        var resultado = PacketParser.TryParse(buffer);

        Assert.Equal(JflParseStatus.Success, resultado.Status);
        Assert.Equal(pacote1.Length, resultado.BytesConsumed);
        Assert.Equal(0x01, resultado.Packet!.Seq);

        // Chamando de novo a partir de onde parou, deve decodificar o segundo pacote.
        var segundoResultado = PacketParser.TryParse(buffer.AsSpan(resultado.BytesConsumed));
        Assert.Equal(JflParseStatus.Success, segundoResultado.Status);
        Assert.Equal(0x02, segundoResultado.Packet!.Seq);
    }
}
