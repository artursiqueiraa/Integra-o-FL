using CentralHub.SDK.Jfl.Messages.Status;

namespace CentralHub.SDK.Tests.Messages.Status;

public class ProblemFlagsTests
{
    [Fact]
    public void Parse_todos_zerados_nao_deve_marcar_nenhum_problema()
    {
        var problemas = ProblemFlags.Parse(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 });

        Assert.False(problemas.Tamper);
        Assert.False(problemas.Ac);
        Assert.True(problemas.AlimentacaoAcNormal);
        Assert.False(problemas.SupervisaoPgm);
    }

    [Fact]
    public void Parse_bit_AC_deve_ser_byte2_bit7()
    {
        var problemas = ProblemFlags.Parse(new byte[] { 0x00, 0b1000_0000, 0x00, 0x00, 0x00 });

        Assert.True(problemas.Ac);
        Assert.False(problemas.AlimentacaoAcNormal);
        Assert.False(problemas.Sirene); // demais bits do byte2 continuam zerados
    }

    [Fact]
    public void Parse_bit_Tamper_deve_ser_byte1_bit3()
    {
        var problemas = ProblemFlags.Parse(new byte[] { 0b0000_1000, 0x00, 0x00, 0x00, 0x00 });

        Assert.True(problemas.Tamper);
    }

    [Fact]
    public void Parse_bits_reservados_do_byte5_nao_devem_afetar_TamperTeclado_e_SupervisaoPgm()
    {
        // Byte5 = 0xFF: bits 0-5 reservados (ignorados), bit6=TamperTeclado, bit7=SupervisaoPgm.
        var problemas = ProblemFlags.Parse(new byte[] { 0x00, 0x00, 0x00, 0x00, 0xFF });

        Assert.True(problemas.TamperTeclado);
        Assert.True(problemas.SupervisaoPgm);
    }

    [Fact]
    public void Parse_com_tamanho_diferente_de_5_deve_lancar()
    {
        Assert.Throws<ArgumentException>(() => ProblemFlags.Parse(new byte[] { 0x00, 0x00 }));
    }
}
