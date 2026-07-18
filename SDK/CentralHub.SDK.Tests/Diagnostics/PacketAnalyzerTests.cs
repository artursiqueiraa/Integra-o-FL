using CentralHub.SDK.Jfl.Diagnostics;

namespace CentralHub.SDK.Tests.Diagnostics;

public class PacketAnalyzerTests
{
    [Fact]
    public void Analisar_KeepAliveRequest_SemDados()
    {
        // Captura real do manual (secao 3.5): 7B 05 18 40 26
        var pacote = Convert.FromHexString("7B05184026");
        var resultado = PacketAnalyzer.Analisar(pacote);

        Assert.True(resultado.CabecalhoValido);
        Assert.True(resultado.ChecksumValido);
        Assert.Equal("KeepAlive", resultado.CmdNome);
        Assert.Empty(resultado.Avisos);
        Assert.Contains(resultado.Campos, c => c.Nome == "DADOS" && c.ValorInterpretado == "(vazio)");
    }

    [Fact]
    public void Analisar_KeepAliveResponse_DecodificaKeep()
    {
        // Captura real do manual (secao 3.5): 7B 06 18 40 00 25
        var pacote = Convert.FromHexString("7B0618400025");
        var resultado = PacketAnalyzer.Analisar(pacote);

        Assert.True(resultado.ChecksumValido);
        var campoKeep = resultado.Campos.Single(c => c.Nome == "KEEP");
        Assert.Contains("1 minuto", campoKeep.Descricao);
    }

    [Fact]
    public void Analisar_ConexaoReal_DecodificaNumeroSerieEModelo()
    {
        // Captura real do manual (secao 3.5), comando de conexao de uma Active 20 Ethernet
        // (TX[102], 102 bytes totais incluindo CAB/QDE/SEQ/CMD/K — conferido token a token
        // contra o texto do manual para bater exatamente com o QDE=0x66=102 declarado).
        byte[] pacote =
        [
            0x7B, 0x66, 0x17, 0x21,
            0x32, 0x37, 0x33, 0x35, 0x38, 0x37, 0x39, 0x32, 0x35, 0x34, // NS (10)
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // IMEI (15)
            0x39, 0x38, 0x46, 0x34, 0x41, 0x42, 0x36, 0x45, 0x46, 0x34, 0x46, 0x30, // MAC (12)
            0xA3, // MOD (1)
            0x36, 0x30, 0x30, // VER (3)
            0x01, // IP (1)
            0x01, // SIMCARD (1)
            0x01, // VIA (1)
            0x06, // OPE (1)
            0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x02, // STATUS (52) - inicio
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x04, 0x01,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // STATUS - fim
            0x41, // K
        ];

        var resultado = PacketAnalyzer.Analisar(pacote);

        Assert.True(resultado.ChecksumValido);
        Assert.Equal("Conexao", resultado.CmdNome);
        var ns = resultado.Campos.Single(c => c.Nome == "NS");
        Assert.Equal("2735879254", ns.ValorInterpretado);
        var mod = resultado.Campos.Single(c => c.Nome == "MOD");
        Assert.Equal("0xA3 (163)", mod.ValorInterpretado);
    }

    [Fact]
    public void Analisar_RespostaTelaMonitorar_DecompoeParticoesPgmsProblemas()
    {
        // Payload sintetico de 113 bytes no formato 4.10 (KP=2, HORA=6, BAT=1, PGM=1,
        // PART=16 a partir do offset 10) com a particao 1 armada.
        var dados = new byte[113];
        dados[10] = 0x02; // PART[0] = particao 1 -> 0x02 = Armada
        var pacote = new byte[4 + dados.Length + 1];
        pacote[0] = 0x7B;
        pacote[1] = (byte)pacote.Length;
        pacote[2] = 0x43;
        pacote[3] = 0x4E;
        Array.Copy(dados, 0, pacote, 4, dados.Length);
        byte k = 0;
        foreach (var b in pacote.AsSpan(0, pacote.Length - 1)) k ^= b;
        pacote[^1] = k;

        var resultado = PacketAnalyzer.Analisar(pacote);

        Assert.True(resultado.ChecksumValido);
        Assert.Contains(resultado.Campos, c => c.Nome == "Partição 1" && c.ValorInterpretado == "Armada");
        Assert.Contains(resultado.Campos, c => c.Nome == "Problemas" && c.ValorInterpretado == "Nenhum");
    }

    [Fact]
    public void Analisar_ChecksumInvalido_SinalizaAviso()
    {
        var pacote = Convert.FromHexString("7B051840FF"); // ultimo byte (checksum) errado de proposito
        var resultado = PacketAnalyzer.Analisar(pacote);

        Assert.False(resultado.ChecksumValido);
        Assert.Contains(resultado.Avisos, a => a.Contains("Checksum inválido"));
    }

    [Fact]
    public void Analisar_CmdDesconhecido_SinalizaAviso()
    {
        var pacote = new byte[] { 0x7B, 0x05, 0x01, 0xEE, 0x00 };
        var k = (byte)(pacote[0] ^ pacote[1] ^ pacote[2] ^ pacote[3]);
        pacote[4] = k;

        var resultado = PacketAnalyzer.Analisar(pacote);

        Assert.Contains(resultado.Avisos, a => a.Contains("não está catalogado"));
    }

    [Fact]
    public void Analisar_CabecalhoInvalido_NaoLancaExcecao()
    {
        var resultado = PacketAnalyzer.Analisar([0x00, 0x01, 0x02]);

        Assert.False(resultado.CabecalhoValido);
        Assert.NotEmpty(resultado.Avisos);
    }

    [Fact]
    public void AnalisarHex_AceitaEspacosEPrefixo()
    {
        var resultado = PacketAnalyzer.AnalisarHex("7B 05 18 40 26");

        Assert.True(resultado.CabecalhoValido);
        Assert.True(resultado.ChecksumValido);
    }

    [Fact]
    public void Analisar_InibirZonas_DecompoeBitmapMsbFirst()
    {
        // Captura real do manual (secao 4.11): "INIBIR ZONAS DE 01 A 04" -> bitmap 0xF0.
        byte[] dados = [0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
        var pacote = new byte[4 + dados.Length + 1];
        pacote[0] = 0x7B;
        pacote[1] = (byte)pacote.Length;
        pacote[2] = 0x50;
        pacote[3] = 0x52;
        Array.Copy(dados, 0, pacote, 4, dados.Length);
        byte k = 0;
        foreach (var b in pacote.AsSpan(0, pacote.Length - 1)) k ^= b;
        pacote[^1] = k;

        var resultado = PacketAnalyzer.Analisar(pacote);

        var campo = resultado.Campos.Single(c => c.Nome == "ZONA (bitmap)");
        Assert.Equal("1, 2, 3, 4", campo.ValorInterpretado);
    }
}
