namespace CentralHub.SDK.Jfl.Messages.Status;

/// <summary>Byte ELET da resposta 4.10 — codigos proprios, diferentes do SELET usado no comando 0x93 (secao 3.2.1).</summary>
public enum ElectrifierState : byte
{
    Desarmado = 0x01,
    Armado = 0x02,
    DesarmadoEmDisparo = 0x81,
    ArmadoEmDisparo = 0x82,
}

/// <summary>Estado do eletrificador (byte ELET) e suas permissoes remotas (byte P-ELET).</summary>
public sealed class ElectrifierStatus
{
    /// <summary><c>null</c> quando ELET e 0x00 (nao programado) ou outro valor nao documentado.</summary>
    public ElectrifierState? Estado { get; init; }

    /// <summary>P-ELET bit 0: permissao para desarmar.</summary>
    public required bool PermiteDesarmar { get; init; }

    /// <summary>P-ELET bit 3: permissao para armar AWAY.</summary>
    public required bool PermiteArmarAway { get; init; }

    internal static ElectrifierStatus Parse(byte estadoBruto, byte permissoesBrutas) => new()
    {
        Estado = Enum.IsDefined(typeof(ElectrifierState), estadoBruto) ? (ElectrifierState)estadoBruto : null,
        PermiteDesarmar = (permissoesBrutas & 0b0000_0001) != 0,
        PermiteArmarAway = (permissoesBrutas & 0b0000_1000) != 0,
    };
}
