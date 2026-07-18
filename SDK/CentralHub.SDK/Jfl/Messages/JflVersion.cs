namespace CentralHub.SDK.Jfl.Messages;

/// <summary>
/// Formata o campo VER (3 bytes ASCII, formato V.S.X) descrito na secao 3.1 do
/// protocolo. Quando X e '0', o beta/X e ocultado (ex.: "400" vira "4.0", nao "4.0.0").
/// </summary>
internal static class JflVersion
{
    public static string Format(ReadOnlySpan<byte> tresBytesAscii)
    {
        if (tresBytesAscii.Length != 3)
        {
            throw new ArgumentException("Campo de versao deve ter exatamente 3 bytes.", nameof(tresBytesAscii));
        }

        var v = (char)tresBytesAscii[0];
        var s = (char)tresBytesAscii[1];
        var x = (char)tresBytesAscii[2];

        return x == '0' ? $"{v}.{s}" : $"{v}.{s}.{x}";
    }
}
