using CentralHub.SDK.Jfl.Protocol;

namespace CentralHub.SDK.Jfl.Messages;

/// <summary>
/// Payload (campo Dados) do comando de conexao 0x21/0x2A, secao 3.1 do protocolo:
/// NS(10) + IMEI(15) + MAC(12) + MOD(1) + VER(3) + IP(1) + SIMCARD(1) + VIA(1) + OPE(1) + STATUS(variavel).
/// </summary>
public sealed class ConnectionRequest
{
    private const int TamanhoNs = 10;
    private const int TamanhoImei = 15;
    private const int TamanhoMac = 12;
    private const int TamanhoModelo = 1;
    private const int TamanhoVersao = 3;
    private const int TamanhoIp = 1;
    private const int TamanhoSimCard = 1;
    private const int TamanhoVia = 1;
    private const int TamanhoOperadora = 1;

    private const int TamanhoMinimo =
        TamanhoNs + TamanhoImei + TamanhoMac + TamanhoModelo + TamanhoVersao +
        TamanhoIp + TamanhoSimCard + TamanhoVia + TamanhoOperadora;

    /// <summary>Numero de serie do equipamento — unico e nunca vazio; usado para correlacionar sessoes.</summary>
    public required string NumeroSerie { get; init; }

    /// <summary>IMEI do modulo celular, ou <c>null</c> quando o equipamento nao possui/usa este campo.</summary>
    public string? Imei { get; init; }

    /// <summary>Endereco MAC do modulo Ethernet, ou <c>null</c> quando o equipamento nao possui/usa este campo.</summary>
    public string? Mac { get; init; }

    /// <summary>Byte MOD — ver <see cref="JflModel"/>.</summary>
    public required byte Modelo { get; init; }

    /// <summary>Versao de firmware formatada (ex.: "4.0.2").</summary>
    public required string Versao { get; init; }

    public required byte EnderecoIp { get; init; }

    public required byte SimCard { get; init; }

    /// <summary>0x00 = GPRS, 0x01 = Ethernet.</summary>
    public required byte Via { get; init; }

    public required byte Operadora { get; init; }

    /// <summary>
    /// Bytes finais do comando de conexao (equivalente ao payload do comando de status,
    /// item 3.2). O formato varia por modelo/versao e nao e decodificado aqui — fica
    /// para uma implementacao futura da camada de status (fora do escopo desta base).
    /// </summary>
    public required byte[] StatusPayload { get; init; }

    public static ConnectionRequest Parse(ReadOnlySpan<byte> dados)
    {
        if (dados.Length < TamanhoMinimo)
        {
            throw new JflProtocolException(
                $"Comando de conexao com dados insuficientes: recebido {dados.Length} bytes, esperado no minimo {TamanhoMinimo}.");
        }

        var offset = 0;

        var numeroSerie = JflText.ReadAsciiOrEmpty(dados.Slice(offset, TamanhoNs));
        offset += TamanhoNs;

        if (string.IsNullOrEmpty(numeroSerie))
        {
            throw new JflProtocolException("Numero de serie vazio no comando de conexao (campo NS nunca deve ser vazio).");
        }

        var imei = JflText.ReadAsciiOrEmpty(dados.Slice(offset, TamanhoImei));
        offset += TamanhoImei;

        var mac = JflText.ReadAsciiOrEmpty(dados.Slice(offset, TamanhoMac));
        offset += TamanhoMac;

        var modelo = dados[offset];
        offset += TamanhoModelo;

        var versao = JflVersion.Format(dados.Slice(offset, TamanhoVersao));
        offset += TamanhoVersao;

        var enderecoIp = dados[offset];
        offset += TamanhoIp;

        var simCard = dados[offset];
        offset += TamanhoSimCard;

        var via = dados[offset];
        offset += TamanhoVia;

        var operadora = dados[offset];
        offset += TamanhoOperadora;

        var statusPayload = dados[offset..].ToArray();

        return new ConnectionRequest
        {
            NumeroSerie = numeroSerie,
            Imei = imei,
            Mac = mac,
            Modelo = modelo,
            Versao = versao,
            EnderecoIp = enderecoIp,
            SimCard = simCard,
            Via = via,
            Operadora = operadora,
            StatusPayload = statusPayload,
        };
    }
}
