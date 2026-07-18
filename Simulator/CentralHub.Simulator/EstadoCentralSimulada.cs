namespace CentralHub.Simulator;

/// <summary>
/// Estado mutavel de uma Active 100 Bus simulada, injetavel pelo chamador (testes, Stress
/// Test, uso manual) — espelha, na direcao de escrita, exatamente os campos que
/// <c>CentralStatusResponse.Parse</c> (SDK, lado servidor) le na secao 4.10 do protocolo.
/// Nao reaproveita nem duplica logica do parser homologado: e o lado "montar bytes" de um
/// formato que o SDK so precisa "ler bytes".
/// </summary>
public sealed class EstadoCentralSimulada
{
    public const int QuantidadeParticoes = 16;
    public const int QuantidadeZonas = 99;

    public DateTime DataHora { get; set; } = DateTime.Now;

    /// <summary>Byte BAT bruto (0x00=sem bateria, 1-100=lítio %, 101-210=chumbo, 0xFF=carregando).</summary>
    public byte Bateria { get; set; } = 0x64; // lítio 100%

    /// <summary>16 posições (1-16): true = PGM acionada.</summary>
    public bool[] PgmsAcionadas { get; } = new bool[16];

    /// <summary>16 posições (1-16): true = usuário tem permissão nesta PGM.</summary>
    public bool[] PgmsPermitidas { get; } = CriarArrayTrue(16);

    /// <summary>16 posições (1-16): byte PART bruto (0x01=Desarmada por padrão).</summary>
    public byte[] Particoes { get; } = CriarArrayPreenchido(16, 0x01);

    /// <summary>16 posições (1-16): byte P-PART bruto (permissões: desarmar+armar+stay+away+pronta = 0b0001_1111).</summary>
    public byte[] ParticoesPermissoes { get; } = CriarArrayPreenchido(16, 0b0001_1111);

    /// <summary>Byte ELET bruto (0x01=desarmado por padrão).</summary>
    public byte Eletrificador { get; set; } = 0x01;

    /// <summary>Byte P-ELET bruto.</summary>
    public byte EletrificadorPermissoes { get; set; } = 0b0000_1001;

    /// <summary>99 posições (1-99): nibble ZoneState bruto (0=desabilitada por padrão).</summary>
    public byte[] Zonas { get; } = new byte[99];

    /// <summary>13 bytes: bitmap P-INIB bruto (LSB-first — mesma convenção da resposta de status). Default: todas permitidas.</summary>
    public byte[] ZonasInibicaoPermitida { get; } = CriarArrayPreenchido(13, 0xFF);

    /// <summary>5 bytes: bitmap PROB bruto (default: nenhum problema).</summary>
    public byte[] Problemas { get; } = new byte[5];

    private static bool[] CriarArrayTrue(int tamanho)
    {
        var array = new bool[tamanho];
        Array.Fill(array, true);
        return array;
    }

    private static byte[] CriarArrayPreenchido(int tamanho, byte valor)
    {
        var array = new byte[tamanho];
        Array.Fill(array, valor);
        return array;
    }

    private static byte ParaBcd(int valorDecimal) => (byte)(((valorDecimal / 10) << 4) | (valorDecimal % 10));

    /// <summary>Monta o payload de 115 bytes (formato 4.10, com PGM2/P-PGM2) que a "central" devolve a qualquer comando da tela monitorar.</summary>
    public byte[] MontarRespostaTelaMonitorar()
    {
        var dados = new byte[115];

        // KP (offset 0-1): nao usar, zero.

        dados[2] = ParaBcd(DataHora.Day);
        dados[3] = ParaBcd(DataHora.Month);
        dados[4] = ParaBcd(DataHora.Year % 100);
        dados[5] = ParaBcd(DataHora.Hour);
        dados[6] = ParaBcd(DataHora.Minute);
        dados[7] = ParaBcd(DataHora.Second);

        dados[8] = Bateria;

        dados[9] = MontarBitmap(PgmsAcionadas, 0, 8);

        for (var i = 0; i < QuantidadeParticoes; i++)
        {
            dados[10 + i] = Particoes[i];
        }

        dados[26] = Eletrificador;

        Array.Copy(MontarZonas(), 0, dados, 27, 50);

        Array.Copy(Problemas, 0, dados, 77, 5);

        dados[82] = EletrificadorPermissoes;
        dados[83] = MontarBitmap(PgmsPermitidas, 0, 8);

        for (var i = 0; i < QuantidadeParticoes; i++)
        {
            dados[84 + i] = ParticoesPermissoes[i];
        }

        Array.Copy(ZonasInibicaoPermitida, 0, dados, 100, 13);

        dados[113] = MontarBitmap(PgmsAcionadas, 8, 8);
        dados[114] = MontarBitmap(PgmsPermitidas, 8, 8);

        return dados;
    }

    private static byte MontarBitmap(bool[] valores, int inicio, int quantidade)
    {
        byte resultado = 0;
        for (var i = 0; i < quantidade; i++)
        {
            if (valores[inicio + i])
            {
                resultado |= (byte)(1 << i);
            }
        }

        return resultado;
    }

    private byte[] MontarZonas()
    {
        var bytes = new byte[50];
        for (var numero = 1; numero <= QuantidadeZonas; numero++)
        {
            var indiceByte = (numero - 1) / 2;
            var ehPrimeiroDoPar = (numero - 1) % 2 == 0;
            var nibble = (byte)(Zonas[numero - 1] & 0x0F);

            if (ehPrimeiroDoPar)
            {
                bytes[indiceByte] = (byte)((nibble << 4) | (bytes[indiceByte] & 0x0F));
            }
            else
            {
                bytes[indiceByte] = (byte)((bytes[indiceByte] & 0xF0) | nibble);
            }
        }

        return bytes;
    }
}
