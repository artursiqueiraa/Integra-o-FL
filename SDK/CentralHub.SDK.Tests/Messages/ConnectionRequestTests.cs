using CentralHub.SDK.Jfl.Messages;
using CentralHub.SDK.Jfl.Protocol;

namespace CentralHub.SDK.Tests.Messages;

public class ConnectionRequestTests
{
    /// <summary>
    /// Campo Dados (97 bytes) extraido byte a byte da captura real de uma Active 20
    /// Ethernet no manual oficial (secao 3.5): "TX[102]=7B 66 17 21 32 37 33 35 38 37
    /// 39 32 35 34 FF...FF 39 38 46 34 41 42 36 45 46 34 46 30 A3 36 30 30 01 01 01 06 ...".
    /// </summary>
    private static readonly byte[] DadosCapturaReal =
    [
        // NS = "2735879254"
        0x32, 0x37, 0x33, 0x35, 0x38, 0x37, 0x39, 0x32, 0x35, 0x34,
        // IMEI vazio (15x 0xFF)
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        // MAC = "98F4AB6EF4F0"
        0x39, 0x38, 0x46, 0x34, 0x41, 0x42, 0x36, 0x45, 0x46, 0x34, 0x46, 0x30,
        // MOD = Active 20 Ethernet
        0xA3,
        // VER = "600" -> 6.0
        0x36, 0x30, 0x30,
        // IP, SIMCARD, VIA, OPE
        0x01, 0x01, 0x01, 0x06,
        // STATUS (52 bytes, payload opaco nesta base de infraestrutura)
        0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x02, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00,
    ];

    [Fact]
    public void Parse_deve_decodificar_a_captura_real_do_manual_corretamente()
    {
        var requisicao = ConnectionRequest.Parse(DadosCapturaReal);

        Assert.Equal("2735879254", requisicao.NumeroSerie);
        Assert.Null(requisicao.Imei); // campo todo 0xFF -> vazio
        Assert.Equal("98F4AB6EF4F0", requisicao.Mac);
        Assert.Equal((byte)JflModel.Active20Ethernet, requisicao.Modelo);
        Assert.Equal("6.0", requisicao.Versao);
        Assert.Equal(0x01, requisicao.EnderecoIp);
        Assert.Equal(0x01, requisicao.SimCard);
        Assert.Equal(0x01, requisicao.Via); // Ethernet
        Assert.Equal(0x06, requisicao.Operadora); // "Nao existe" (consistente com Ethernet puro)
        Assert.Equal(52, requisicao.StatusPayload.Length);
    }

    [Fact]
    public void Parse_deve_lancar_quando_dados_sao_insuficientes()
    {
        var dadosCurtos = new byte[10];

        Assert.Throws<JflProtocolException>(() => ConnectionRequest.Parse(dadosCurtos));
    }

    [Fact]
    public void Parse_deve_lancar_quando_numero_de_serie_vem_vazio()
    {
        var dados = (byte[])DadosCapturaReal.Clone();
        Array.Fill(dados, (byte)0xFF, 0, 10); // zera o campo NS com 0xFF (vazio)

        Assert.Throws<JflProtocolException>(() => ConnectionRequest.Parse(dados));
    }

    [Fact]
    public void ToNomeAmigavel_deve_reconhecer_a_Active_100_Bus()
    {
        Assert.Equal("Active 100 Bus", ((byte)0xA4).ToNomeAmigavel());
    }

    [Fact]
    public void ToNomeAmigavel_deve_ter_fallback_para_modelo_desconhecido()
    {
        Assert.Equal("Desconhecido (0xF0)", ((byte)0xF0).ToNomeAmigavel());
    }
}
