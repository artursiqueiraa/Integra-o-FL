using System.Collections.Concurrent;

namespace CentralHub.Api.Logging;

/// <summary>
/// <see cref="ILoggerProvider"/> aditivo que captura os logs estruturados que as classes de
/// <c>CentralHub.SDK.Jfl.*</c> já emitem hoje (<c>JflTcpServer</c>, <c>PgmCommandService</c>,
/// <c>CentralStatusQueryService</c>, handlers) — alimenta <see cref="SessionActivityLogService"/>
/// sem tocar em nenhum arquivo do SDK. Só captura categorias que começam com
/// "CentralHub.SDK.Jfl" (ignora ASP.NET/EF Core/etc., para não gerar ruído).
/// </summary>
/// <remarks>
/// Resolve <see cref="SessionActivityLogService"/> de forma preguiçosa (só no primeiro log
/// realmente capturado, não no construtor) de propósito: <see cref="SessionActivityLogService"/>
/// depende de <c>SessionManager</c> (SDK), que por sua vez depende de
/// <c>ILogger&lt;SessionManager&gt;</c> — ou seja, de <c>ILoggerFactory</c> já com todos os
/// providers prontos, este incluído. Resolver a dependência no construtor deste provider criaria
/// uma dependência circular (ILoggerFactory → este provider → SessionActivityLogService →
/// SessionManager → ILogger&lt;SessionManager&gt; → ILoggerFactory). Adiar a resolução para
/// dentro de <see cref="SdkActivityLogger.Log{TState}"/> quebra o ciclo: a essa altura o
/// SessionManager já terminou de ser construído.
/// </remarks>
public sealed class SdkActivityLoggerProvider : ILoggerProvider
{
    private const string PrefixoCategoria = "CentralHub.SDK.Jfl";

    private readonly IServiceProvider _serviceProvider;
    private readonly Lazy<SessionActivityLogService> _logService;
    private readonly ConcurrentDictionary<string, SdkActivityLogger> _loggers = new();

    public SdkActivityLoggerProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logService = new Lazy<SessionActivityLogService>(() => _serviceProvider.GetRequiredService<SessionActivityLogService>());
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (!categoryName.StartsWith(PrefixoCategoria, StringComparison.Ordinal))
        {
            return NullLogger.Instance;
        }

        return _loggers.GetOrAdd(categoryName, nome => new SdkActivityLogger(nome, _logService));
    }

    public void Dispose() => _loggers.Clear();

    private sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
