using CentralHub.SDK.Jfl.Server;
using CentralHub.SDK.Jfl.Server.Handlers;
using CentralHub.SDK.Jfl.Server.Handlers.Stubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CentralHub.SDK.Jfl;

/// <summary>Registra toda a infraestrutura do servidor JFL (sessao, dispatcher, handlers e stubs) no container de DI.</summary>
public static class JflServiceCollectionExtensions
{
    public static IServiceCollection AddJflServer(this IServiceCollection services, Action<JflServerOptions>? configure = null)
    {
        var options = new JflServerOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddSingleton<SessionManager>();
        services.TryAddSingleton<ICentralAuthorizationProvider, LiberarTodasCentraisAuthorizationProvider>();
        services.AddSingleton<CentralStatusQueryService>();
        services.AddSingleton<PgmCommandService>();
        services.AddSingleton<ArmCommandService>();
        services.AddSingleton<ZoneInhibitCommandService>();

        // Comandos de base (implementados de fato):
        services.AddSingleton<IJflCommandHandler, ConnectionCommandHandler>();
        services.AddSingleton<IJflCommandHandler, KeepAliveCommandHandler>();

        // Comandos de negocio (stubs — arquitetura pronta, logica futura):
        services.AddSingleton<IJflCommandHandler, PedirStatusCommandHandlerStub>();
        services.AddSingleton<IJflCommandHandler, EventoCommandHandlerStub>();
        services.AddSingleton<IJflCommandHandler, StatusCommandHandlerStub>();
        // ArmCommandHandlerStub e ZoneCommandHandlerStub continuam registrados mesmo com
        // ArmCommandService/ZoneInhibitCommandService implementados: como sao respostas do
        // Tipo A (servidor pergunta, central responde), a resposta normal e consumida
        // direto por SendAndWaitAsync/SEQ — estes stubs so capturam pacotes 0x4E/0x4F/0x53/
        // 0x54/0x52 orfaos ou atrasados (mesmo papel que PgmCommandHandlerStub ja cumpre
        // hoje ao lado do PgmCommandService real).
        services.AddSingleton<IJflCommandHandler, ArmCommandHandlerStub>();
        services.AddSingleton<IJflCommandHandler, PgmCommandHandlerStub>();
        services.AddSingleton<IJflCommandHandler, ZoneCommandHandlerStub>();
        services.AddSingleton<IJflCommandHandler, AtualizarDataHoraCommandHandlerStub>();
        services.AddSingleton<IJflCommandHandler, PasswordCommandHandlerStub>();

        services.AddSingleton<JflCommandDispatcher>();
        services.AddSingleton<JflTcpServer>();

        return services;
    }
}
