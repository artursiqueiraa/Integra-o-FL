using System.Diagnostics;
using System.Net.Sockets;
using CentralHub.SDK.Jfl.Protocol;
using CentralHub.SDK.Jfl.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Necessario explicitamente porque AddJflServer e um metodo de extensao definido no
// namespace CentralHub.SDK.Jfl (JflServiceCollectionExtensions), fora do namespace deste
// arquivo (CentralHub.SDK.Jfl.Diagnostics).
using CentralHub.SDK.Jfl;

namespace CentralHub.SDK.Jfl.Diagnostics;

public sealed class ReplayResultado
{
    public required byte[] PacoteEnviado { get; init; }

    public JflPacket? RespostaRecebida { get; init; }

    public required bool Sucesso { get; init; }

    public string? Erro { get; init; }

    public TimeSpan Duracao { get; init; }
}

/// <summary>
/// Reproduz uma captura de pacote (tipicamente um dos arquivos de
/// <c>Documentation/RealCaptures/</c>) contra um servidor JFL real via TCP — nunca abre uma
/// sessao "falsa" nem contorna <see cref="SessionManager"/>/<see cref="JflTcpServer"/>; envia
/// os bytes exatamente como capturados por um <see cref="TcpClient"/> comum, do mesmo jeito
/// que uma central real faria. Util para reproduzir bugs de forma deterministica.
/// </summary>
public static class ReplayEngine
{
    private static readonly TimeSpan TimeoutPadrao = TimeSpan.FromSeconds(10);

    /// <summary>Le os bytes de um arquivo de captura (ex.: <c>Documentation/RealCaptures/PGM_ON.bin</c>) e reproduz.</summary>
    public static async Task<ReplayResultado> ReplayArquivoAsync(
        string caminhoBin, string host, int porta, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        var bytes = await File.ReadAllBytesAsync(caminhoBin, cancellationToken).ConfigureAwait(false);
        return await ReplayAsync(bytes, host, porta, cancellationToken, timeout).ConfigureAwait(false);
    }

    /// <summary>Envia o pacote bruto para <paramref name="host"/>:<paramref name="porta"/> e aguarda uma resposta completa.</summary>
    public static async Task<ReplayResultado> ReplayAsync(
        byte[] pacoteBruto, string host, int porta, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        var cronometro = Stopwatch.StartNew();
        using var timeoutCts = new CancellationTokenSource(timeout ?? TimeoutPadrao);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, porta, linkedCts.Token).ConfigureAwait(false);

            var stream = client.GetStream();
            await stream.WriteAsync(pacoteBruto, linkedCts.Token).ConfigureAwait(false);

            var reader = new JflFrameReader(stream);
            var resposta = await reader.ReadPacketAsync(linkedCts.Token).ConfigureAwait(false);
            cronometro.Stop();

            return new ReplayResultado
            {
                PacoteEnviado = pacoteBruto,
                RespostaRecebida = resposta,
                Sucesso = resposta is not null,
                Erro = resposta is null ? "A conexão foi encerrada antes de uma resposta completa." : null,
                Duracao = cronometro.Elapsed,
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            cronometro.Stop();
            return new ReplayResultado
            {
                PacoteEnviado = pacoteBruto, Sucesso = false, Erro = "Timeout aguardando resposta.", Duracao = cronometro.Elapsed,
            };
        }
        catch (SocketException ex)
        {
            cronometro.Stop();
            return new ReplayResultado
            {
                PacoteEnviado = pacoteBruto, Sucesso = false, Erro = $"Falha de conexão: {ex.Message}", Duracao = cronometro.Elapsed,
            };
        }
    }

    /// <summary>
    /// Sobe um <see cref="JflTcpServer"/> efêmero (porta 0, mesma técnica de
    /// <c>JflTcpServerIntegrationTests</c>) usando a mesma extensão de DI
    /// (<c>AddJflServer</c>) que o Backend real usa, faz o replay contra ele, e derruba o
    /// servidor em seguida — permite reproduzir bugs sem precisar do Backend real rodando.
    /// </summary>
    public static async Task<ReplayResultado> ReplayContraServidorEfemeroAsync(
        byte[] pacoteBruto, CancellationToken cancellationToken, TimeSpan? timeout = null, Action<JflServerOptions>? configurar = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddJflServer(options =>
        {
            options.Porta = 0;
            configurar?.Invoke(options);
        });

        await using var provider = services.BuildServiceProvider();
        var server = provider.GetRequiredService<JflTcpServer>();
        server.Start();

        try
        {
            return await ReplayAsync(pacoteBruto, "127.0.0.1", server.Port, cancellationToken, timeout).ConfigureAwait(false);
        }
        finally
        {
            await server.StopAsync().ConfigureAwait(false);
        }
    }
}
