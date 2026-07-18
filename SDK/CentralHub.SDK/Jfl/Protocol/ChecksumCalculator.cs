namespace CentralHub.SDK.Jfl.Protocol;

/// <summary>
/// Checksum do protocolo JFL: "ou exclusiva" (XOR) entre todos os bytes do pacote,
/// incluindo o cabecalho. Ao XORar um pacote completo (incluindo o proprio byte de
/// checksum), o resultado deve ser zero (secao 2.5 do protocolo).
/// </summary>
public static class ChecksumCalculator
{
    /// <summary>Calcula o byte de checksum (K) a partir dos bytes do pacote, sem incluir o proprio K.</summary>
    public static byte Calculate(ReadOnlySpan<byte> bytesSemChecksum)
    {
        byte resultado = 0;
        foreach (var b in bytesSemChecksum)
        {
            resultado ^= b;
        }

        return resultado;
    }

    /// <summary>Valida um pacote completo (incluindo o byte de checksum): o XOR de tudo deve dar zero.</summary>
    public static bool IsValid(ReadOnlySpan<byte> pacoteCompleto) => Calculate(pacoteCompleto) == 0;
}
