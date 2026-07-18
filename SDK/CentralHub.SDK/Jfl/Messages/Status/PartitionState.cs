namespace CentralHub.SDK.Jfl.Messages.Status;

/// <summary>Byte PART da resposta 4.10 ("tela monitorar"). Valores fora desta lista devem ser tratados como desabilitada.</summary>
public enum PartitionState : byte
{
    Desarmada = 0x01,
    Armada = 0x02,
    ArmadaStay = 0x03,
    DesarmadaEmDisparo = 0x81,
    ArmadaEmDisparo = 0x82,
    ArmadaStayEmDisparo = 0x83,
}

/// <summary>Estado de uma particao (1 a 16) e suas permissoes remotas (byte P-PART correspondente).</summary>
public sealed class PartitionStatus
{
    public required int Numero { get; init; }

    /// <summary><c>null</c> quando o byte PART e 0x00 (nao programada) ou outro valor nao documentado (tratar como desabilitada).</summary>
    public PartitionState? Estado { get; init; }

    public bool Desabilitada => Estado is null;

    public required bool PermiteDesarmar { get; init; }

    public required bool PermiteArmar { get; init; }

    public required bool PermiteArmarStay { get; init; }

    public required bool PermiteArmarAway { get; init; }

    public required bool Pronta { get; init; }

    internal static PartitionState? ParseEstado(byte valor) =>
        Enum.IsDefined(typeof(PartitionState), valor) ? (PartitionState)valor : null;

    internal static PartitionStatus Parse(int numero, byte estadoBruto, byte permissoesBrutas) => new()
    {
        Numero = numero,
        Estado = ParseEstado(estadoBruto),
        PermiteDesarmar = (permissoesBrutas & 0b0000_0001) != 0,
        PermiteArmar = (permissoesBrutas & 0b0000_0010) != 0,
        PermiteArmarStay = (permissoesBrutas & 0b0000_0100) != 0,
        PermiteArmarAway = (permissoesBrutas & 0b0000_1000) != 0,
        Pronta = (permissoesBrutas & 0b0001_0000) != 0,
    };
}
