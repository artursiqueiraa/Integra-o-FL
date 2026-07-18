namespace CentralHub.SDK.Jfl.Messages.Status;

/// <summary>Nibble do campo ZONA da resposta 4.10. Valores 9-15 nao sao documentados.</summary>
public enum ZoneState : byte
{
    Desabilitada = 0,
    Inibida = 1,
    Disparo = 2,
    SemComunicacao = 3,
    Curto = 4,
    TamperAberto = 5,
    BateriaBaixa = 6,
    Aberta = 7,
    Fechada = 8,
}

/// <summary>Estado de uma zona (1 a 99) e se ela pode ser inibida remotamente (bit correspondente em P-INIB).</summary>
public sealed class ZoneStatus
{
    public required int Numero { get; init; }

    /// <summary><c>null</c> quando o nibble nao corresponde a nenhum valor documentado (9-15).</summary>
    public ZoneState? Estado { get; init; }

    public required bool PermiteInibir { get; init; }

    internal static ZoneState? ParseEstado(byte nibble) =>
        Enum.IsDefined(typeof(ZoneState), nibble) ? (ZoneState)nibble : null;
}
