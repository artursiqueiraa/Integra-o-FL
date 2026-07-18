using System.Text;

namespace CentralHub.StressTest;

/// <summary>Coleta métricas durante a execução do Stress Test e gera o relatório em Markdown.</summary>
public sealed class RelatorioStress
{
    public List<double> LatenciasConexaoMs { get; } = [];
    public List<double> LatenciasKeepAliveMs { get; } = [];
    public List<double> LatenciasEventoMs { get; } = [];
    public List<double> LatenciasConsultaMs { get; } = [];
    public List<double> LatenciasPgmMs { get; } = [];

    public int FalhasConexao { get; set; }
    public int FalhasKeepAlive { get; set; }
    public int FalhasEvento { get; set; }
    public int FalhasConsulta { get; set; }
    public int FalhasPgm { get; set; }

    public bool ReconexaoOk { get; set; }
    public bool CenarioTimeoutRetornouFalhaCorretamente { get; set; }
    public bool CenariosDeFalhaSinalizados { get; set; }

    public long MemoriaAntesBytes { get; set; }
    public long MemoriaDepoisBytes { get; set; }

    private static (double Media, double P95, double P99, double Max) Estatisticas(List<double> valores)
    {
        if (valores.Count == 0) return (0, 0, 0, 0);
        var ordenados = valores.OrderBy(v => v).ToList();
        double Percentil(double p)
        {
            var indice = (int)Math.Ceiling(p * ordenados.Count) - 1;
            return ordenados[Math.Clamp(indice, 0, ordenados.Count - 1)];
        }

        return (valores.Average(), Percentil(0.95), Percentil(0.99), ordenados[^1]);
    }

    public string GerarResumoConsole()
    {
        var (mediaConexao, p95Conexao, _, _) = Estatisticas(LatenciasConexaoMs);
        var (mediaPgm, p95Pgm, _, _) = Estatisticas(LatenciasPgmMs);
        return $"\nResumo: conexão média {mediaConexao:F1}ms (p95 {p95Conexao:F1}ms), " +
               $"PGM média {mediaPgm:F1}ms (p95 {p95Pgm:F1}ms), " +
               $"memória {(MemoriaDepoisBytes - MemoriaAntesBytes) / 1024.0 / 1024.0:F2} MB delta.";
    }

    public string GerarMarkdown(OpcoesStress opcoes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Stress Test — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("Executado contra um `JflTcpServer` efêmero (mesma infraestrutura homologada do ");
        sb.AppendLine("Backend, via `AddJflServer`) usando o Central Simulator — sem hardware físico.");
        sb.AppendLine();
        sb.AppendLine("## Parâmetros");
        sb.AppendLine();
        sb.AppendLine($"- Conexões: {opcoes.Conexoes}");
        sb.AppendLine($"- Keep-alives: {opcoes.KeepAlives}");
        sb.AppendLine($"- Eventos: {opcoes.Eventos}");
        sb.AppendLine($"- Consultas de status: {opcoes.Consultas}");
        sb.AppendLine($"- Comandos de PGM: {opcoes.Pgms}");
        sb.AppendLine();
        sb.AppendLine("## Latência por categoria (ms)");
        sb.AppendLine();
        sb.AppendLine("| Categoria | Amostras | Média | P95 | P99 | Máx |");
        sb.AppendLine("|---|---|---|---|---|---|");
        AppendLinha(sb, "Conexão", LatenciasConexaoMs);
        AppendLinha(sb, "Keep-alive", LatenciasKeepAliveMs);
        AppendLinha(sb, "Evento", LatenciasEventoMs);
        AppendLinha(sb, "Consulta de status", LatenciasConsultaMs);
        AppendLinha(sb, "PGM", LatenciasPgmMs);
        sb.AppendLine();
        sb.AppendLine("## Falhas");
        sb.AppendLine();
        sb.AppendLine($"- Conexão: {FalhasConexao}");
        sb.AppendLine($"- Keep-alive: {FalhasKeepAlive}");
        sb.AppendLine($"- Evento sem confirmação: {FalhasEvento} " +
                       "(esperado nesta fase — 0x24 ainda é stub até a Fase 1 implementar o handler real)");
        sb.AppendLine($"- Consulta de status: {FalhasConsulta}");
        sb.AppendLine($"- PGM: {FalhasPgm}");
        sb.AppendLine();
        sb.AppendLine("## Cenários de falha (reconexão, timeout, checksum inválido, pacote quebrado)");
        sb.AppendLine();
        sb.AppendLine($"- Reconexão após desconexão simulada: {(ReconexaoOk ? "OK" : "FALHOU")}");
        sb.AppendLine($"- Timeout de comando (PGM para central que não responde) retornou falha corretamente: {(CenarioTimeoutRetornouFalhaCorretamente ? "OK" : "FALHOU")}");
        sb.AppendLine($"- Checksum inválido / pacote quebrado: enviados e descartados sem exceção não tratada: {(CenariosDeFalhaSinalizados ? "OK" : "NÃO EXECUTADO")}");
        sb.AppendLine();
        sb.AppendLine("## Memória (processo desta ferramenta, não do Backend)");
        sb.AppendLine();
        sb.AppendLine($"- Antes: {MemoriaAntesBytes / 1024.0 / 1024.0:F2} MB");
        sb.AppendLine($"- Depois: {MemoriaDepoisBytes / 1024.0 / 1024.0:F2} MB");
        sb.AppendLine($"- Delta: {(MemoriaDepoisBytes - MemoriaAntesBytes) / 1024.0 / 1024.0:F2} MB");

        return sb.ToString();
    }

    private static void AppendLinha(StringBuilder sb, string nome, List<double> valores)
    {
        var (media, p95, p99, max) = Estatisticas(valores);
        sb.AppendLine($"| {nome} | {valores.Count} | {media:F2} | {p95:F2} | {p99:F2} | {max:F2} |");
    }
}
