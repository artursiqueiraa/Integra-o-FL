namespace CentralHub.SDK.Adapters;

/// <remarks>
/// <b>[Obsoleto]</b> Modelo antigo de discagem de saída — não representa mais como a
/// integração real funciona (a central disca para o CentralHub, nunca o contrário; ver
/// <c>Documentation/ARQUITETURA_SESSION_MANAGER.md</c>). Mantido só porque
/// <c>OperationService</c> (fluxo legado, já simulado) ainda usa <c>AcionarPGM</c>/
/// <c>DesligarPGM</c>/<c>PulsoPGM</c> (métodos stub, sem I/O real).
/// </remarks>
public class IntelbrasAdapter : ICentralAdapter
{
    /// <summary>Porta padrão de fábrica do receptor IP Intelbras (AMT), usada como heurística de detecção.</summary>
    public const int PortaPadrao = 9009;

    [Obsolete("Discagem de saida nao e mais usada pela arquitetura real (SessionManager). Ver Documentation/ARQUITETURA_SESSION_MANAGER.md.")]
    public async Task<bool> Conectar(string ip, int porta, string usuario, string senha)
    {
        var (sucesso, _, _) = await TcpConnectionHelper.TestarConexao(ip, porta);
        return sucesso;
    }

    [Obsolete("Discagem de saida nao e mais usada pela arquitetura real (SessionManager). Ver Documentation/ARQUITETURA_SESSION_MANAGER.md.")]
    public async Task<ConexaoResult> VerificarConectividade(string ip, int porta, string usuario, string senha)
    {
        var (sucesso, latenciaMs, erro) = await TcpConnectionHelper.TestarConexao(ip, porta);

        if (!sucesso)
        {
            return new ConexaoResult
            {
                Sucesso = false,
                Status = "Offline",
                LatenciaMs = latenciaMs,
                Erro = erro
            };
        }

        // TODO: identificar Modelo/Firmware reais via protocolo proprietario Intelbras.
        return new ConexaoResult
        {
            Sucesso = true,
            Fabricante = "Intelbras",
            Modelo = "AMT 8000",
            Firmware = "2.4.1",
            Status = "Online",
            LatenciaMs = latenciaMs
        };
    }

    public Task<ComandoResult> AcionarPGM(string ip, int porta, string usuario, string senha, int pgm)
    {
        // TODO: implementar comando real do protocolo Intelbras.
        return Task.FromResult(new ComandoResult { Sucesso = true, Resultado = $"PGM {pgm} ligado" });
    }

    public Task<ComandoResult> DesligarPGM(string ip, int porta, string usuario, string senha, int pgm)
    {
        // TODO: implementar comando real do protocolo Intelbras.
        return Task.FromResult(new ComandoResult { Sucesso = true, Resultado = $"PGM {pgm} desligado" });
    }

    public Task<ComandoResult> PulsoPGM(string ip, int porta, string usuario, string senha, int pgm, int tempoMs)
    {
        // TODO: implementar comando real do protocolo Intelbras.
        return Task.FromResult(new ComandoResult { Sucesso = true, Resultado = $"PGM {pgm} pulso de {tempoMs}ms" });
    }
}
