namespace CentralHub.SDK.Jfl.Messages.Status;

public enum BatteryType
{
    SemBateria,
    Litio,
    Chumbo,
    Carregando,

    /// <summary>Faixa 211-254: documentada como "reservado, nao usar".</summary>
    Reservado,
}

/// <summary>
/// Byte BAT da resposta 4.10. A JFL documenta duas faixas de bateria no mesmo byte
/// (litio em % e chumbo em tensao) mais os valores especiais 0x00 (sem bateria) e
/// 0xFF (carregando).
/// </summary>
public sealed class BatteryStatus
{
    public required byte ValorBruto { get; init; }

    public required BatteryType Tipo { get; init; }

    /// <summary>0 a 100. So preenchido quando <see cref="Tipo"/> e <see cref="BatteryType.Litio"/>.</summary>
    public int? PercentualLitio { get; init; }

    /// <summary>
    /// Tensao aproximada em Volts, interpolada linearmente dentro da faixa 101-210 =
    /// 7,2V-15,0V documentada pela JFL (o manual nao da uma formula exata, so os
    /// extremos da faixa — trate este valor como aproximado). So preenchido quando
    /// <see cref="Tipo"/> e <see cref="BatteryType.Chumbo"/>.
    /// </summary>
    public double? TensaoChumboAproximada { get; init; }

    private const byte LitioMin = 0x01;
    private const byte LitioMax = 0x64;
    private const byte ChumboMin = 0x65;
    private const byte ChumboMax = 0xD2;
    private const double ChumboTensaoMin = 7.2;
    private const double ChumboTensaoMax = 15.0;

    public static BatteryStatus Parse(byte valor)
    {
        if (valor == 0x00)
        {
            return new BatteryStatus { ValorBruto = valor, Tipo = BatteryType.SemBateria };
        }

        if (valor == 0xFF)
        {
            return new BatteryStatus { ValorBruto = valor, Tipo = BatteryType.Carregando };
        }

        if (valor is >= LitioMin and <= LitioMax)
        {
            return new BatteryStatus { ValorBruto = valor, Tipo = BatteryType.Litio, PercentualLitio = valor };
        }

        if (valor is >= ChumboMin and <= ChumboMax)
        {
            var fracao = (valor - (double)ChumboMin) / (ChumboMax - ChumboMin);
            var tensao = ChumboTensaoMin + (fracao * (ChumboTensaoMax - ChumboTensaoMin));
            return new BatteryStatus { ValorBruto = valor, Tipo = BatteryType.Chumbo, TensaoChumboAproximada = Math.Round(tensao, 2) };
        }

        return new BatteryStatus { ValorBruto = valor, Tipo = BatteryType.Reservado };
    }
}
