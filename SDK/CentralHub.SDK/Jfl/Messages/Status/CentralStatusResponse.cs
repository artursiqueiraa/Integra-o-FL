using CentralHub.SDK.Jfl.Protocol;

namespace CentralHub.SDK.Jfl.Messages.Status;

/// <summary>
/// Payload (campo Dados) da resposta ao comando de status como superusuario (0x4D,
/// secao 4.1), no formato "RESPOSTA DOS COMANDOS DA TELA MONITORAR" descrito na
/// secao 4.10 do protocolo. E a mesma resposta usada por qualquer comando da tela
/// monitorar (armar, desarmar, PGM, zonas...) — aqui e usada especificamente para a
/// consulta de status (comando 0x4D sem dados).
/// </summary>
/// <remarks>
/// Dimensionado para a Active 100 Bus: 16 particoes (fixo no protocolo, PART/P-PART
/// sempre tem 16 bytes) e 99 zonas (o campo ZONA cabe ate 100, mas a Active 100 Bus
/// documenta 99). PGM2/P-PGM2 (PGMs 9-16) sao tratados como opcionais no fim do
/// pacote — produtos/firmwares mais antigos nao os enviam (secao 2.4.1: o software
/// receptor deve tolerar pacotes menores de versoes anteriores); quando ausentes,
/// as PGMs 9-16 sao reportadas como desligadas/sem permissao.
/// </remarks>
public sealed class CentralStatusResponse
{
    public const int QuantidadeParticoes = 16;
    public const int QuantidadeZonas = 99;

    private const int TamanhoKp = 2;
    private const int TamanhoHora = 6;
    private const int TamanhoBat = 1;
    private const int TamanhoPgm = 1;
    private const int TamanhoPart = 16;
    private const int TamanhoElet = 1;
    private const int TamanhoZona = 50;
    private const int TamanhoProb = 5;
    private const int TamanhoPElet = 1;
    private const int TamanhoPPgm = 1;
    private const int TamanhoPPart = 16;
    private const int TamanhoPInib = 13;

    /// <summary>Tamanho minimo (ate P-INIB inclusive); PGM2/P-PGM2/RESERVADO sao opcionais.</summary>
    private const int TamanhoMinimo =
        TamanhoKp + TamanhoHora + TamanhoBat + TamanhoPgm + TamanhoPart + TamanhoElet +
        TamanhoZona + TamanhoProb + TamanhoPElet + TamanhoPPgm + TamanhoPPart + TamanhoPInib;

    /// <summary>
    /// Data e hora informadas pela propria central (campo HORA). <c>null</c> quando
    /// os bytes recebidos nao formam uma data valida (ex.: central sem hora ajustada).
    /// </summary>
    public DateTime? DataHoraCentral { get; init; }

    public required BatteryStatus Bateria { get; init; }

    /// <summary>16 posicoes, numeradas 1 a 16 (combina PGM+PGM2 com P-PGM+P-PGM2).</summary>
    public required IReadOnlyList<PgmStatus> Pgms { get; init; }

    /// <summary>16 posicoes, numeradas 1 a 16.</summary>
    public required IReadOnlyList<PartitionStatus> Particoes { get; init; }

    public required ElectrifierStatus Eletrificador { get; init; }

    /// <summary>99 posicoes, numeradas 1 a 99.</summary>
    public required IReadOnlyList<ZoneStatus> Zonas { get; init; }

    public required ProblemFlags Problemas { get; init; }

    public static CentralStatusResponse Parse(ReadOnlySpan<byte> dados)
    {
        if (dados.Length < TamanhoMinimo)
        {
            throw new JflProtocolException(
                $"Resposta de status com dados insuficientes: recebido {dados.Length} bytes, esperado no minimo {TamanhoMinimo}.");
        }

        var offset = TamanhoKp; // KP nao e usado (documentado como "nao usar").

        var horaBytes = dados.Slice(offset, TamanhoHora);
        offset += TamanhoHora;

        var bat = dados[offset];
        offset += TamanhoBat;

        var pgm1a8 = dados[offset];
        offset += TamanhoPgm;

        var part = dados.Slice(offset, TamanhoPart);
        offset += TamanhoPart;

        var elet = dados[offset];
        offset += TamanhoElet;

        var zona = dados.Slice(offset, TamanhoZona);
        offset += TamanhoZona;

        var prob = dados.Slice(offset, TamanhoProb);
        offset += TamanhoProb;

        var pElet = dados[offset];
        offset += TamanhoPElet;

        var pPgm1a8 = dados[offset];
        offset += TamanhoPPgm;

        var pPart = dados.Slice(offset, TamanhoPPart);
        offset += TamanhoPPart;

        var pInib = dados.Slice(offset, TamanhoPInib);
        offset += TamanhoPInib;

        // PGM2/P-PGM2: opcionais (ausentes em produtos/firmwares que nao chegam a 16 PGMs).
        var pgm9a16 = dados.Length > offset ? dados[offset] : (byte)0x00;
        var pPgm9a16 = dados.Length > offset + 1 ? dados[offset + 1] : (byte)0x00;

        var pgms = new List<PgmStatus>(16);
        pgms.AddRange(PgmStatus.ParseFaixa(pgm1a8, pPgm1a8, numeroInicial: 1));
        pgms.AddRange(PgmStatus.ParseFaixa(pgm9a16, pPgm9a16, numeroInicial: 9));

        var particoes = new List<PartitionStatus>(QuantidadeParticoes);
        for (var i = 0; i < QuantidadeParticoes; i++)
        {
            particoes.Add(PartitionStatus.Parse(numero: i + 1, estadoBruto: part[i], permissoesBrutas: pPart[i]));
        }

        var zonas = ParseZonas(zona, pInib);

        return new CentralStatusResponse
        {
            DataHoraCentral = ParseDataHora(horaBytes),
            Bateria = BatteryStatus.Parse(bat),
            Pgms = pgms,
            Particoes = particoes,
            Eletrificador = ElectrifierStatus.Parse(elet, pElet),
            Zonas = zonas,
            Problemas = ProblemFlags.Parse(prob),
        };
    }

    private static DateTime? ParseDataHora(ReadOnlySpan<byte> seisBytes)
    {
        try
        {
            var dia = JflBcd.ToDecimal(seisBytes[0]);
            var mes = JflBcd.ToDecimal(seisBytes[1]);
            var ano = 2000 + JflBcd.ToDecimal(seisBytes[2]);
            var hora = JflBcd.ToDecimal(seisBytes[3]);
            var minuto = JflBcd.ToDecimal(seisBytes[4]);
            var segundo = JflBcd.ToDecimal(seisBytes[5]);

            return new DateTime(ano, mes, dia, hora, minuto, segundo, DateTimeKind.Unspecified);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <remarks>
    /// Ordem dos nibbles dentro de cada byte de ZONA nao tem exemplo numerico no
    /// manual (ao contrario de CONTA/VERSAO/DATA, que tem exemplos explicitos).
    /// Aqui a zona de numero mais baixo do par fica no nibble mais significativo,
    /// seguindo a mesma convencao usada em todos os outros campos "por nibble"
    /// documentados. Confirmar contra hardware real antes de depender disso em
    /// producao.
    /// </remarks>
    private static List<ZoneStatus> ParseZonas(ReadOnlySpan<byte> zonaBytes, ReadOnlySpan<byte> pInibBytes)
    {
        var zonas = new List<ZoneStatus>(QuantidadeZonas);

        for (var numero = 1; numero <= QuantidadeZonas; numero++)
        {
            var indiceByte = (numero - 1) / 2;
            var ehPrimeiroDoPar = (numero - 1) % 2 == 0;
            var nibble = ehPrimeiroDoPar
                ? (byte)(zonaBytes[indiceByte] >> 4)
                : (byte)(zonaBytes[indiceByte] & 0x0F);

            var indiceByteInib = (numero - 1) / 8;
            var indiceBitInib = (numero - 1) % 8;
            var permiteInibir = (pInibBytes[indiceByteInib] & (1 << indiceBitInib)) != 0;

            zonas.Add(new ZoneStatus
            {
                Numero = numero,
                Estado = ZoneStatus.ParseEstado(nibble),
                PermiteInibir = permiteInibir,
            });
        }

        return zonas;
    }
}
