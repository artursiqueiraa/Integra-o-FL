using CentralHub.SDK.Jfl.Server;

namespace CentralHub.Api.Services;

/// <summary>Inicia e encerra o servidor TCP JFL junto com o ciclo de vida da aplicacao ASP.NET Core.</summary>
public class JflServerHostedService : IHostedService
{
    private readonly JflTcpServer _server;
    private readonly ILogger<JflServerHostedService> _logger;

    public JflServerHostedService(JflTcpServer server, ILogger<JflServerHostedService> logger)
    {
        _server = server;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _server.Start();
        _logger.LogInformation("JflServerHostedService iniciado (servidor TCP JFL na porta {Port})", _server.Port);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => _server.StopAsync();
}
