using System.Diagnostics;
using System.Net.Sockets;

namespace CentralHub.SDK.Adapters;

/// <summary>
/// Helper interno usado pelos adapters para testar a conectividade TCP real
/// com a central (IP/Porta) e medir a latencia da conexao.
/// A identificacao de fabricante/modelo/firmware depende do protocolo
/// proprietario de cada central e deve ser implementada em cada adapter.
/// </summary>
/// <remarks>
/// <b>[Obsoleto]</b> Este helper implementa o modelo de arquitetura antigo, onde o
/// CentralHub discava (TcpClient de saida) para a central. A arquitetura real hoje e o
/// inverso: a central disca para o CentralHub (TcpListener + SessionManager, ver
/// SDK/CentralHub.SDK/Jfl/Server). Mantido apenas porque OperationService (fluxo legado,
/// ja simulado/mockado) ainda depende de AdapterFactory/ICentralAdapter para o mesmo tipo
/// (JflAdapter/IntelbrasAdapter), que por sua vez chamam este helper em
/// VerificarConectividade. Nao usar em codigo novo — para qualquer comando real, usar
/// SessionManager/JflSession (ver Documentation/ARQUITETURA_SESSION_MANAGER.md).
/// </remarks>
internal static class TcpConnectionHelper
{
    private const int TimeoutMs = 3000;

    [Obsolete("Discagem de saida (TcpClient) nao e mais usada pela arquitetura real (SessionManager). " +
        "Mantido só para o fluxo legado de OperationService. Ver Documentation/ARQUITETURA_SESSION_MANAGER.md.")]
    public static async Task<(bool sucesso, long latenciaMs, string? erro)> TestarConexao(string ip, int porta)
    {
        using var client = new TcpClient();
        var cronometro = Stopwatch.StartNew();

        try
        {
            var conexaoTask = client.ConnectAsync(ip, porta);
            var timeoutTask = Task.Delay(TimeoutMs);

            var completada = await Task.WhenAny(conexaoTask, timeoutTask);
            cronometro.Stop();

            if (completada == timeoutTask || !client.Connected)
            {
                return (false, cronometro.ElapsedMilliseconds, "Tempo limite de conexao excedido");
            }

            return (true, cronometro.ElapsedMilliseconds, null);
        }
        catch (Exception ex)
        {
            cronometro.Stop();
            return (false, cronometro.ElapsedMilliseconds, ex.Message);
        }
    }
}
