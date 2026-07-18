namespace CentralHub.SDK.Jfl.Protocol;

/// <summary>
/// Pacote 0x7B ja decodificado: CAB e QDE sao apenas framing (nao carregam
/// informacao de negocio) e por isso nao aparecem aqui - so Seq, Cmd e Dados.
/// </summary>
public sealed class JflPacket
{
    public required byte Seq { get; init; }

    public required byte Cmd { get; init; }

    public required byte[] Dados { get; init; }

    public override string ToString() =>
        $"Seq=0x{Seq:X2} Cmd=0x{Cmd:X2} Dados=[{Convert.ToHexString(Dados)}]";
}
