using CentralHub.SDK.Jfl.Messages;

namespace CentralHub.SDK.Tests.Messages;

public class JflVersionTests
{
    [Theory]
    [InlineData(new byte[] { 0x32, 0x37, 0x31 }, "2.7.1")]
    [InlineData(new byte[] { 0x34, 0x30, 0x32 }, "4.0.2")]
    [InlineData(new byte[] { 0x39, 0x39, 0x39 }, "9.9.9")]
    [InlineData(new byte[] { 0x34, 0x30, 0x30 }, "4.0")] // X='0' oculta o terceiro digito
    public void Format_deve_bater_com_os_exemplos_do_manual(byte[] bytes, string esperado)
    {
        Assert.Equal(esperado, JflVersion.Format(bytes));
    }

    [Fact]
    public void Format_deve_exigir_exatamente_3_bytes()
    {
        Assert.Throws<ArgumentException>(() => JflVersion.Format(new byte[] { 0x34, 0x30 }));
    }
}
