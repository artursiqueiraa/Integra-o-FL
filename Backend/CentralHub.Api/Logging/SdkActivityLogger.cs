namespace CentralHub.Api.Logging;

/// <summary>
/// <see cref="ILogger"/> que extrai as propriedades nomeadas já presentes em cada chamada
/// <c>_logger.LogInformation("...{Prop}...", valor)</c> das classes do SDK (structured
/// logging — não faz parsing de texto/regex) e repassa para
/// <see cref="SessionActivityLogService"/>. Não altera, não intercepta, não modifica
/// nenhum comportamento do SDK — só observa o que ele já loga hoje.
/// </summary>
public sealed class SdkActivityLogger : ILogger
{
    private readonly string _categoria;
    private readonly Lazy<SessionActivityLogService> _logService;

    public SdkActivityLogger(string categoria, Lazy<SessionActivityLogService> logService)
    {
        _categoria = categoria;
        _logService = logService;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string? remoteEndPoint = null;
        string? remoteIp = null;
        string? remotePorta = null;
        string? numeroSerie = null;
        byte? cmd = null;
        byte? seq = null;
        int? bytesRecebidos = null;
        int? bytesEnviados = null;
        double? tempoMs = null;

        if (state is IReadOnlyList<KeyValuePair<string, object>> propriedades)
        {
            foreach (var (chave, valor) in propriedades)
            {
                switch (chave)
                {
                    case "RemoteEndPoint":
                        remoteEndPoint = valor?.ToString();
                        break;
                    case "RemoteIp":
                        remoteIp = valor?.ToString();
                        break;
                    case "RemotePort":
                    case "RemotePorta":
                        remotePorta = valor?.ToString();
                        break;
                    case "NumeroSerie":
                        numeroSerie = valor?.ToString();
                        break;
                    case "Cmd":
                        cmd = ParaByte(valor);
                        break;
                    case "Seq":
                        seq = ParaByte(valor);
                        break;
                    case "BytesRecebidos":
                        bytesRecebidos = ParaInt(valor);
                        break;
                    case "BytesEnviados":
                        bytesEnviados = ParaInt(valor);
                        break;
                    case "TempoMs":
                    case "TempoRespostaMs":
                        tempoMs = ParaDouble(valor);
                        break;
                }
            }
        }

        remoteEndPoint ??= remoteIp is not null && remotePorta is not null ? $"{remoteIp}:{remotePorta}" : null;

        _logService.Value.Registrar(new AtividadeLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Nivel = logLevel,
            Categoria = _categoria,
            Mensagem = formatter(state, exception),
            RemoteEndPoint = remoteEndPoint,
            NumeroSerie = numeroSerie,
            Cmd = cmd,
            Seq = seq,
            BytesRecebidos = bytesRecebidos,
            BytesEnviados = bytesEnviados,
            TempoRespostaMs = tempoMs,
        });
    }

    private static byte? ParaByte(object? valor)
    {
        try
        {
            return valor is null ? null : Convert.ToByte(valor);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or InvalidCastException)
        {
            return null;
        }
    }

    private static int? ParaInt(object? valor)
    {
        try
        {
            return valor is null ? null : Convert.ToInt32(valor);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or InvalidCastException)
        {
            return null;
        }
    }

    private static double? ParaDouble(object? valor)
    {
        try
        {
            return valor is null ? null : Convert.ToDouble(valor);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or InvalidCastException)
        {
            return null;
        }
    }
}
