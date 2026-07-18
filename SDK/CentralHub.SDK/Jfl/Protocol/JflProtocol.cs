namespace CentralHub.SDK.Jfl.Protocol;

/// <summary>
/// Constantes do protocolo de comunicacao JFL com cabecalho 0x7B, usado pela
/// Active 100 Bus (placa PCI-350, todas as versoes) e demais equipamentos da
/// linha Active/M-300 listados no documento "Protocolo de comunicacao dos
/// equipamentos JFL com a estacao monitoramento".
/// </summary>
public static class JflProtocol
{
    /// <summary>Byte de cabecalho ('{'). Todo pacote 0x7B comeca com este byte.</summary>
    public const byte Header0x7B = 0x7B;

    /// <summary>
    /// Tamanho minimo de um pacote 0x7B: CAB(1) + QDE(1) + SEQ(1) + CMD(1) + K(1),
    /// ou seja, um comando sem nenhum byte de dados.
    /// </summary>
    public const int MinPacketLength = 5;

    /// <summary>Tamanho maximo de um pacote 0x7B (o byte QDE tem 1 byte de tamanho).</summary>
    public const int MaxPacketLength = 255;
}
