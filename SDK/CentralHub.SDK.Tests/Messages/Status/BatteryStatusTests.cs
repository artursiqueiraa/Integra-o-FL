using CentralHub.SDK.Jfl.Messages.Status;

namespace CentralHub.SDK.Tests.Messages.Status;

public class BatteryStatusTests
{
    [Fact]
    public void Parse_0x00_deve_ser_sem_bateria()
    {
        var bateria = BatteryStatus.Parse(0x00);

        Assert.Equal(BatteryType.SemBateria, bateria.Tipo);
        Assert.Null(bateria.PercentualLitio);
        Assert.Null(bateria.TensaoChumboAproximada);
    }

    [Fact]
    public void Parse_0xFF_deve_ser_carregando()
    {
        Assert.Equal(BatteryType.Carregando, BatteryStatus.Parse(0xFF).Tipo);
    }

    [Theory]
    [InlineData(0x01, 1)]
    [InlineData(0x64, 100)]
    [InlineData(0x32, 50)]
    public void Parse_faixa_de_litio_deve_reportar_o_percentual_igual_ao_valor(byte valor, int percentualEsperado)
    {
        var bateria = BatteryStatus.Parse(valor);

        Assert.Equal(BatteryType.Litio, bateria.Tipo);
        Assert.Equal(percentualEsperado, bateria.PercentualLitio);
    }

    [Fact]
    public void Parse_extremo_inferior_de_chumbo_deve_ser_aproximadamente_7_2V()
    {
        var bateria = BatteryStatus.Parse(0x65); // 101 decimal

        Assert.Equal(BatteryType.Chumbo, bateria.Tipo);
        Assert.NotNull(bateria.TensaoChumboAproximada);
        Assert.Equal(7.2, bateria.TensaoChumboAproximada!.Value, precision: 2);
    }

    [Fact]
    public void Parse_extremo_superior_de_chumbo_deve_ser_aproximadamente_15V()
    {
        var bateria = BatteryStatus.Parse(0xD2); // 210 decimal

        Assert.Equal(BatteryType.Chumbo, bateria.Tipo);
        Assert.Equal(15.0, bateria.TensaoChumboAproximada!.Value, precision: 2);
    }

    [Theory]
    [InlineData(0xD3)] // 211
    [InlineData(0xFE)] // 254
    public void Parse_faixa_reservada_nao_deve_lancar(byte valor)
    {
        var bateria = BatteryStatus.Parse(valor);

        Assert.Equal(BatteryType.Reservado, bateria.Tipo);
    }
}
