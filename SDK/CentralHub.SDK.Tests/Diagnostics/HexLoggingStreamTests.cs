using CentralHub.SDK.Jfl.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace CentralHub.SDK.Tests.Diagnostics;

public class HexLoggingStreamTests
{
    [Fact]
    public async Task WriteAsync_deve_repassar_os_bytes_sem_alterar_nada()
    {
        var destino = new MemoryStream();
        var decorator = new HexLoggingStream(destino, NullLogger.Instance, "teste");
        byte[] dados = [0x7B, 0x05, 0x18, 0x40, 0x26];

        await decorator.WriteAsync(dados);

        Assert.Equal(dados, destino.ToArray());
    }

    [Fact]
    public async Task ReadAsync_deve_devolver_exatamente_os_bytes_do_stream_interno()
    {
        byte[] dados = [0x7B, 0x06, 0x18, 0x40, 0x00, 0x25];
        var origem = new MemoryStream(dados);
        var decorator = new HexLoggingStream(origem, NullLogger.Instance, "teste");

        var buffer = new byte[10];
        var lidos = await decorator.ReadAsync(buffer.AsMemory(0, buffer.Length));

        Assert.Equal(dados.Length, lidos);
        Assert.Equal(dados, buffer.AsSpan(0, lidos).ToArray());
    }

    [Fact]
    public async Task Comportamento_deve_ser_identico_com_e_sem_logging_habilitado()
    {
        byte[] dados = [0x01, 0x02, 0x03, 0x04];

        var destinoSemLog = new MemoryStream();
        await new HexLoggingStream(destinoSemLog, NullLogger.Instance, "sem-log").WriteAsync(dados);

        var destinoComLog = new MemoryStream();
        var loggerHabilitado = new LoggerFalsoSempreHabilitado();
        await new HexLoggingStream(destinoComLog, loggerHabilitado, "com-log").WriteAsync(dados);

        Assert.Equal(destinoSemLog.ToArray(), destinoComLog.ToArray());
    }

    /// <summary>Logger mínimo que sempre reporta Debug habilitado, só para exercitar o caminho de log sem depender de infraestrutura externa.</summary>
    private sealed class LoggerFalsoSempreHabilitado : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
