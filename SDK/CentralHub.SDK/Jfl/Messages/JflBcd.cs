namespace CentralHub.SDK.Jfl.Messages;

/// <summary>
/// Decodifica campos "por nibble" (BCD): os 4 bits mais significativos representam
/// o primeiro digito decimal e os 4 menos significativos o segundo (ex.: 0x46 = 46).
/// </summary>
internal static class JflBcd
{
    public static int ToDecimal(byte valor) => ((valor >> 4) * 10) + (valor & 0x0F);
}
