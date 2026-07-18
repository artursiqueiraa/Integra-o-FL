namespace CentralHub.SDK.Jfl.Messages.Status;

/// <summary>
/// Campo PROB (5 bytes / 40 bits) da resposta 4.10. Cada propriedade e um problema
/// documentado; bit 1 = problema presente, bit 0 = normal (mesma convencao do byte
/// PROB de 1 byte usado em outras respostas do protocolo).
/// </summary>
public sealed class ProblemFlags
{
    // Byte 1
    public required bool BateriaFracaControleOuSensorSemFio { get; init; }
    public required bool SupervisaoSensor { get; init; }
    public required bool SaidaAuxiliar { get; init; }
    public required bool Tamper { get; init; }
    public required bool Dhcp { get; init; }
    public required bool CaboDeRede { get; init; }
    public required bool ModuloCelular { get; init; }
    public required bool Sms { get; init; }

    // Byte 2
    public required bool Ethernet { get; init; }
    public required bool Gprs { get; init; }
    public required bool LinhaTelefonica { get; init; }
    public required bool Curto { get; init; }
    public required bool Teclado { get; init; }
    public required bool Sirene { get; init; }
    public required bool Bateria { get; init; }

    /// <summary>Byte 2, bit 7 — problema na alimentacao AC (energia da rede eletrica).</summary>
    public required bool Ac { get; init; }

    // Byte 3
    public required bool BateriaInvertidaOuEmCurto { get; init; }
    public required bool IpDestino2 { get; init; }
    public required bool IpDestino1 { get; init; }
    public required bool ServidorDns { get; init; }
    public required bool RedeTecladoAc { get; init; }
    public required bool SupervisaoSirene { get; init; }
    public required bool SenhaRedeSemFio { get; init; }
    public required bool AutenticacaoRedeSemFio { get; init; }

    // Byte 4
    public required bool SsidNaoEncontrado { get; init; }
    public required bool ConflitoIp { get; init; }
    public required bool Barramento { get; init; }
    public required bool Ddns { get; init; }
    public required bool Notificacao { get; init; }
    public required bool ModuloEthernet { get; init; }
    public required bool NivelSinalOperadora { get; init; }
    public required bool ChipCelular { get; init; }

    // Byte 5 (bits 0-5 reservados)
    public required bool TamperTeclado { get; init; }
    public required bool SupervisaoPgm { get; init; }

    /// <summary>Alimentacao AC normal (conveniencia: inverso de <see cref="Ac"/>).</summary>
    public bool AlimentacaoAcNormal => !Ac;

    public static ProblemFlags Parse(ReadOnlySpan<byte> cincoBytes)
    {
        if (cincoBytes.Length != 5)
        {
            throw new ArgumentException("O campo PROB deve ter exatamente 5 bytes.", nameof(cincoBytes));
        }

        var b1 = cincoBytes[0];
        var b2 = cincoBytes[1];
        var b3 = cincoBytes[2];
        var b4 = cincoBytes[3];
        var b5 = cincoBytes[4];

        bool Bit(byte b, int i) => (b & (1 << i)) != 0;

        return new ProblemFlags
        {
            BateriaFracaControleOuSensorSemFio = Bit(b1, 0),
            SupervisaoSensor = Bit(b1, 1),
            SaidaAuxiliar = Bit(b1, 2),
            Tamper = Bit(b1, 3),
            Dhcp = Bit(b1, 4),
            CaboDeRede = Bit(b1, 5),
            ModuloCelular = Bit(b1, 6),
            Sms = Bit(b1, 7),

            Ethernet = Bit(b2, 0),
            Gprs = Bit(b2, 1),
            LinhaTelefonica = Bit(b2, 2),
            Curto = Bit(b2, 3),
            Teclado = Bit(b2, 4),
            Sirene = Bit(b2, 5),
            Bateria = Bit(b2, 6),
            Ac = Bit(b2, 7),

            BateriaInvertidaOuEmCurto = Bit(b3, 0),
            IpDestino2 = Bit(b3, 1),
            IpDestino1 = Bit(b3, 2),
            ServidorDns = Bit(b3, 3),
            RedeTecladoAc = Bit(b3, 4),
            SupervisaoSirene = Bit(b3, 5),
            SenhaRedeSemFio = Bit(b3, 6),
            AutenticacaoRedeSemFio = Bit(b3, 7),

            SsidNaoEncontrado = Bit(b4, 0),
            ConflitoIp = Bit(b4, 1),
            Barramento = Bit(b4, 2),
            Ddns = Bit(b4, 3),
            Notificacao = Bit(b4, 4),
            ModuloEthernet = Bit(b4, 5),
            NivelSinalOperadora = Bit(b4, 6),
            ChipCelular = Bit(b4, 7),

            TamperTeclado = Bit(b5, 6),
            SupervisaoPgm = Bit(b5, 7),
        };
    }
}
