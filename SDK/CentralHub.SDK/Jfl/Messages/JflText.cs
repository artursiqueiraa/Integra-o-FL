namespace CentralHub.SDK.Jfl.Messages;

/// <summary>Leitura de campos texto do protocolo (ASCII, preenchidos com 0xFF quando vazios).</summary>
internal static class JflText
{
    /// <summary>Le um campo ASCII de tamanho fixo, retornando <c>null</c> quando todo o campo e 0xFF (vazio).</summary>
    public static string? ReadAsciiOrEmpty(ReadOnlySpan<byte> campo)
    {
        var todosPreenchidoComFF = true;
        foreach (var b in campo)
        {
            if (b != 0xFF)
            {
                todosPreenchidoComFF = false;
                break;
            }
        }

        return todosPreenchidoComFF ? null : System.Text.Encoding.ASCII.GetString(campo);
    }
}
