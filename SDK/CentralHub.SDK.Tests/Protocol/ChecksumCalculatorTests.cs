using CentralHub.SDK.Jfl.Protocol;

namespace CentralHub.SDK.Tests.Protocol;

public class ChecksumCalculatorTests
{
    [Fact]
    public void Calculate_deve_bater_com_o_exemplo_do_manual_JFL()
    {
        // Secao 2.5 do protocolo: 0x7B,0x05,0x18,0x40 -> checksum 0x26.
        byte[] bytes = [0x7B, 0x05, 0x18, 0x40];

        var checksum = ChecksumCalculator.Calculate(bytes);

        Assert.Equal(0x26, checksum);
    }

    [Fact]
    public void IsValid_deve_retornar_true_para_pacote_real_capturado()
    {
        // Keep-alive real capturado no manual: TX[5]=7B 05 18 40 26.
        byte[] pacote = [0x7B, 0x05, 0x18, 0x40, 0x26];

        Assert.True(ChecksumCalculator.IsValid(pacote));
    }

    [Fact]
    public void IsValid_deve_retornar_false_quando_um_byte_e_corrompido()
    {
        byte[] pacote = [0x7B, 0x05, 0x18, 0x41, 0x26]; // CMD alterado de 0x40 para 0x41

        Assert.False(ChecksumCalculator.IsValid(pacote));
    }

    [Fact]
    public void Calculate_de_um_pacote_vazio_deve_ser_zero()
    {
        Assert.Equal(0, ChecksumCalculator.Calculate(ReadOnlySpan<byte>.Empty));
    }
}
