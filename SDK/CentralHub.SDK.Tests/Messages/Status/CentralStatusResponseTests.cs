using CentralHub.SDK.Jfl.Messages.Status;
using CentralHub.SDK.Jfl.Protocol;

namespace CentralHub.SDK.Tests.Messages.Status;

public class CentralStatusResponseTests
{
    /// <summary>
    /// Campo Dados (113 bytes) extraido byte a byte da captura real "RESPOSTA DO
    /// COMANDO DE ARMAR A PARTIÇÃO A (01)" de uma Active 20 Ethernet, secao 4.11 do
    /// manual: "TX[118]=7B 76 42 4E 01 79 21 06 21 11 51 39 00 00 02 01 00...FF 01
    /// 00...00 E0". Essa central nao envia PGM2/P-PGM2 (produto com so 4 PGMs) —
    /// serve como caso real de compatibilidade retroativa (secao 2.4.1 do manual).
    /// </summary>
    private static readonly byte[] DadosCapturaReal_113Bytes =
    [
        0x01, 0x79, 0x21, 0x06, 0x21, 0x11, 0x51, 0x39, 0x00, 0x00, 0x02, 0x01,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x04, 0x77, 0x77, 0x77, 0x77, 0x70, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00, 0x00, 0x00, 0x0F,
        0x1B, 0x1B, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0xFF, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00,
    ];

    [Fact]
    public void Parse_da_captura_real_deve_ter_113_bytes_e_checksum_valido_no_pacote_original()
    {
        Assert.Equal(113, DadosCapturaReal_113Bytes.Length);

        // Reconstroi o pacote original (CAB QDE SEQ CMD DADOS K) e confere o checksum,
        // provando que a transcricao do array acima bate com a captura do manual.
        var pacote = PacketBuilder.Build(seq: 0x42, cmd: 0x4E, DadosCapturaReal_113Bytes);
        Assert.Equal(0x76, pacote[1]); // QDE = 118 decimal = 0x76, igual ao TX[118] do manual
        Assert.Equal(0xE0, pacote[^1]); // checksum do manual
    }

    [Fact]
    public void Parse_da_captura_real_deve_decodificar_data_hora_da_central()
    {
        var status = CentralStatusResponse.Parse(DadosCapturaReal_113Bytes);

        Assert.Equal(new DateTime(2021, 6, 21, 11, 51, 39), status.DataHoraCentral);
    }

    [Fact]
    public void Parse_da_captura_real_deve_decodificar_bateria_como_sem_bateria()
    {
        var status = CentralStatusResponse.Parse(DadosCapturaReal_113Bytes);

        Assert.Equal(BatteryType.SemBateria, status.Bateria.Tipo);
        Assert.Equal(0x00, status.Bateria.ValorBruto);
    }

    [Fact]
    public void Parse_da_captura_real_deve_refletir_a_particao_A_recem_armada()
    {
        var status = CentralStatusResponse.Parse(DadosCapturaReal_113Bytes);

        Assert.Equal(16, status.Particoes.Count);

        var particao1 = status.Particoes[0];
        Assert.Equal(1, particao1.Numero);
        Assert.Equal(PartitionState.Armada, particao1.Estado); // e a resposta do comando "armar particao A"
        Assert.True(particao1.PermiteDesarmar);
        Assert.True(particao1.PermiteArmar);
        Assert.False(particao1.PermiteArmarStay);
        Assert.True(particao1.PermiteArmarAway);
        Assert.True(particao1.Pronta);

        var particao2 = status.Particoes[1];
        Assert.Equal(PartitionState.Desarmada, particao2.Estado);

        // Active 20 Ethernet so tem 2 particoes: as demais vem zeradas (nao programadas).
        for (var i = 2; i < 16; i++)
        {
            Assert.True(status.Particoes[i].Desabilitada);
            Assert.False(status.Particoes[i].PermiteArmar);
        }
    }

    [Fact]
    public void Parse_da_captura_real_deve_expor_16_pgms_com_apenas_1_a_4_permitidas()
    {
        var status = CentralStatusResponse.Parse(DadosCapturaReal_113Bytes);

        Assert.Equal(16, status.Pgms.Count);

        // Active 20 Ethernet so tem 4 PGMs (P-PGM = 0x0F -> bits 0-3).
        for (var i = 0; i < 4; i++)
        {
            Assert.True(status.Pgms[i].Permitida, $"PGM {i + 1} deveria estar permitida");
            Assert.False(status.Pgms[i].Acionada);
        }

        for (var i = 4; i < 16; i++)
        {
            Assert.False(status.Pgms[i].Permitida, $"PGM {i + 1} nao deveria estar permitida");
        }
    }

    [Fact]
    public void Parse_da_captura_real_deve_tratar_PGM2_e_P_PGM2_ausentes_como_zero()
    {
        // O pacote real so tem 113 bytes (sem PGM2/P-PGM2) — confirma a tolerancia
        // a pacotes mais curtos de produtos/firmwares mais antigos (secao 2.4.1).
        var status = CentralStatusResponse.Parse(DadosCapturaReal_113Bytes);

        for (var numero = 9; numero <= 16; numero++)
        {
            var pgm = status.Pgms.Single(p => p.Numero == numero);
            Assert.False(pgm.Acionada);
            Assert.False(pgm.Permitida);
        }
    }

    [Fact]
    public void Parse_da_captura_real_deve_decodificar_eletrificador_com_estado_nao_documentado()
    {
        var status = CentralStatusResponse.Parse(DadosCapturaReal_113Bytes);

        // ELET = 0x04 na captura real, valor que nao consta na tabela do manual
        // (0x00/0x01/0x02/0x81/0x82) — o parser deve reportar Estado=null em vez de
        // lancar excecao ou inventar um significado.
        Assert.Null(status.Eletrificador.Estado);
        Assert.False(status.Eletrificador.PermiteDesarmar);
        Assert.False(status.Eletrificador.PermiteArmarAway);
    }

    [Fact]
    public void Parse_da_captura_real_deve_decodificar_as_9_primeiras_zonas_e_o_restante_desabilitado()
    {
        var status = CentralStatusResponse.Parse(DadosCapturaReal_113Bytes);

        Assert.Equal(99, status.Zonas.Count);

        // ZONA bytes 0x77 0x77 0x77 0x77 0x70 -> nibble 7 = "aberta" (secao 4.10:
        // "Nibble = 7, aberta"), entao zonas 1-9 estao abertas; zona 10 desabilitada.
        for (var numero = 1; numero <= 9; numero++)
        {
            var zona = status.Zonas.Single(z => z.Numero == numero);
            Assert.Equal(ZoneState.Aberta, zona.Estado);
            Assert.True(zona.PermiteInibir); // P-INIB byte1=0xFF (zonas 1-8) e byte2 bit0=1 (zona 9)
        }

        var zona10 = status.Zonas.Single(z => z.Numero == 10);
        Assert.Equal(ZoneState.Desabilitada, zona10.Estado);
        Assert.False(zona10.PermiteInibir);

        for (var numero = 11; numero <= 99; numero++)
        {
            var zona = status.Zonas.Single(z => z.Numero == numero);
            Assert.Equal(ZoneState.Desabilitada, zona.Estado);
            Assert.False(zona.PermiteInibir);
        }
    }

    [Fact]
    public void Parse_da_captura_real_deve_reportar_problemas_de_sirene_e_bateria()
    {
        var status = CentralStatusResponse.Parse(DadosCapturaReal_113Bytes);

        // PROB = 00 60 00 00 00 -> byte2 = 0x60 = bits 5 e 6 (Sirene, Bateria).
        Assert.True(status.Problemas.Sirene);
        Assert.True(status.Problemas.Bateria);
        Assert.False(status.Problemas.Ac);
        Assert.True(status.Problemas.AlimentacaoAcNormal);

        Assert.False(status.Problemas.Tamper);
        Assert.False(status.Problemas.Curto);
        Assert.False(status.Problemas.SupervisaoPgm);
    }

    [Fact]
    public void Parse_deve_lancar_quando_dados_sao_menores_que_o_minimo()
    {
        var dadosCurtos = new byte[50];

        Assert.Throws<JflProtocolException>(() => CentralStatusResponse.Parse(dadosCurtos));
    }

    [Fact]
    public void Parse_deve_ler_PGM2_e_P_PGM2_quando_presentes_no_pacote_Active_100_Bus()
    {
        // Constroi um pacote sintetico "Active 100 Bus" completo: os mesmos 113
        // bytes da captura real, mais PGM2=0xFF (PGMs 9-16 todas acionadas) e
        // P-PGM2=0xFF (todas permitidas).
        var dados = DadosCapturaReal_113Bytes.Concat(new byte[] { 0xFF, 0xFF }).ToArray();

        var status = CentralStatusResponse.Parse(dados);

        Assert.Equal(115, dados.Length);
        for (var numero = 9; numero <= 16; numero++)
        {
            var pgm = status.Pgms.Single(p => p.Numero == numero);
            Assert.True(pgm.Acionada, $"PGM {numero} deveria estar acionada");
            Assert.True(pgm.Permitida, $"PGM {numero} deveria estar permitida");
        }
    }

    [Fact]
    public void Parse_deve_ignorar_bytes_reservados_apos_P_PGM2()
    {
        var dados = DadosCapturaReal_113Bytes
            .Concat(new byte[] { 0x00, 0x00 }) // PGM2, P-PGM2
            .Concat(new byte[] { 0xAA, 0xBB, 0xCC }) // RESERVADO, deve ser ignorado sem lancar
            .ToArray();

        var status = CentralStatusResponse.Parse(dados);

        Assert.Equal(16, status.Pgms.Count);
    }

    [Fact]
    public void Parse_deve_reconhecer_todos_os_estados_de_particao_documentados()
    {
        var dados = (byte[])DadosCapturaReal_113Bytes.Clone();
        // Offset das 16 particoes: KP(2)+HORA(6)+BAT(1)+PGM(1) = 10.
        byte[] estados = [0x01, 0x02, 0x03, 0x81, 0x82, 0x83, 0x00, 0xFE, 0, 0, 0, 0, 0, 0, 0, 0];
        Array.Copy(estados, 0, dados, 10, 16);

        var status = CentralStatusResponse.Parse(dados);

        Assert.Equal(PartitionState.Desarmada, status.Particoes[0].Estado);
        Assert.Equal(PartitionState.Armada, status.Particoes[1].Estado);
        Assert.Equal(PartitionState.ArmadaStay, status.Particoes[2].Estado);
        Assert.Equal(PartitionState.DesarmadaEmDisparo, status.Particoes[3].Estado);
        Assert.Equal(PartitionState.ArmadaEmDisparo, status.Particoes[4].Estado);
        Assert.Equal(PartitionState.ArmadaStayEmDisparo, status.Particoes[5].Estado);
        Assert.True(status.Particoes[6].Desabilitada); // 0x00 = nao programada
        Assert.True(status.Particoes[7].Desabilitada); // 0xFE = nao documentado -> desabilitada
    }
}
