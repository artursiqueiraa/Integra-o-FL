using CentralHub.Api.Data;
using CentralHub.Api.Logging;
using CentralHub.Api.Models;
using CentralHub.Api.Services;
using CentralHub.SDK.Jfl;
using CentralHub.SDK.Jfl.Server;
using CentralHub.Simulator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CentralHub.Api.Tests;

/// <summary>
/// Valida <see cref="CentralSessionService"/> contra um <see cref="JflTcpServer"/> real (porta
/// efêmera) e o Central Simulator (<c>Simulator/CentralHub.Simulator</c>) — prova que
/// "Status da Conexão"/"Reconectar"/"Diagnóstico" refletem a sessão de verdade, sem mocks.
/// </summary>
public class CentralSessionServiceTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private JflTcpServer _server = null!;
    private int _buildingId;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        // Nivel precisa incluir Debug/Information: e nesse nivel que o SDK
        // (CentralHub.SDK.Jfl.*) loga Cmd/Seq/BytesRecebidos/BytesEnviados/TempoRespostaMs —
        // com o padrao (Warning) o SdkActivityLoggerProvider nunca recebe essas entradas.
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));

        // O nome precisa ser gerado UMA vez fora do lambda: AddDbContext reavalia o
        // optionsAction a cada novo scope, então Guid.NewGuid() ali dentro faria cada
        // scope enxergar um banco em memória diferente (sintoma: "Central X não encontrada"
        // mesmo logo após criá-la em outro scope).
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddJflServer(options => options.Porta = 0);
        services.AddSingleton<SessionActivityLogService>();
        services.AddScoped<CentralSessionService>();
        // Mesmo registro aditivo/preguicoso de Program.cs: sem isso, nenhum log do SDK e
        // capturado neste container de teste (ILoggerFactory nao teria o provider customizado).
        services.AddSingleton<ILoggerProvider>(sp => new SdkActivityLoggerProvider(sp));

        _provider = services.BuildServiceProvider();

        // Mesmo efeito de AddHostedService, sem precisar do host completo do ASP.NET Core.
        var logService = _provider.GetRequiredService<SessionActivityLogService>();
        await logService.StartAsync(CancellationToken.None);

        _server = _provider.GetRequiredService<JflTcpServer>();
        _server.Start();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var building = new Building { Nome = "Prédio Teste" };
        context.Buildings.Add(building);
        await context.SaveChangesAsync();
        _buildingId = building.Id;
    }

    public async Task DisposeAsync()
    {
        await _server.StopAsync();
        await _provider.GetRequiredService<SessionActivityLogService>().StopAsync(CancellationToken.None);
        await _provider.DisposeAsync();
    }

    private async Task<int> CriarCentralAsync(string? numeroSerie)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var central = new Central { Nome = "Central Teste", BuildingId = _buildingId, NumeroSerie = numeroSerie };
        context.Centrals.Add(central);
        await context.SaveChangesAsync();
        return central.Id;
    }

    private CentralSessionService ObterService(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<CentralSessionService>();

    [Fact]
    public async Task ObterSessaoAsync_sem_conexao_deve_retornar_Offline()
    {
        var centralId = await CriarCentralAsync("3000000001");

        using var scope = _provider.CreateScope();
        var sessao = await ObterService(scope).ObterSessaoAsync(centralId, CancellationToken.None);

        Assert.Equal("Offline", sessao.StatusConexao);
        Assert.False(sessao.SessaoAtiva);
        Assert.True(sessao.CentralCadastrada);
    }

    [Fact]
    public async Task ObterSessaoAsync_com_simulador_conectado_deve_retornar_Online()
    {
        var centralId = await CriarCentralAsync("3000000002");

        await using var simulador = new SimuladorActive100Bus("3000000002");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        using var scope = _provider.CreateScope();
        var sessao = await ObterService(scope).ObterSessaoAsync(centralId, CancellationToken.None);

        Assert.Equal("Online", sessao.StatusConexao);
        Assert.True(sessao.SessaoAtiva);
        Assert.True(sessao.SocketConectado);
        Assert.True(sessao.HandshakeRealizado);
        Assert.Equal("Active 100 Bus", sessao.Modelo);
        Assert.False(sessao.NumeroSerieDivergente);
    }

    [Fact]
    public async Task ObterSessaoAsync_apos_desconexao_deve_voltar_a_Offline()
    {
        var centralId = await CriarCentralAsync("3000000003");

        var simulador = new SimuladorActive100Bus("3000000003");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);
        simulador.SimularDesconexao();
        await Task.Delay(500); // tempo do servidor detectar o fechamento e remover a sessao

        using var scope = _provider.CreateScope();
        var sessao = await ObterService(scope).ObterSessaoAsync(centralId, CancellationToken.None);

        Assert.Equal("Offline", sessao.StatusConexao);
        await simulador.DisposeAsync();
    }

    [Fact]
    public async Task ReconectarAsync_com_sessao_ativa_deve_encerrar_e_sumir_do_SessionManager()
    {
        var centralId = await CriarCentralAsync("3000000004");

        await using var simulador = new SimuladorActive100Bus("3000000004");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        var sessionManager = _provider.GetRequiredService<SessionManager>();
        Assert.True(sessionManager.TryGet("3000000004", out _));

        using var scope = _provider.CreateScope();
        var resultado = await ObterService(scope).ReconectarAsync(centralId, CancellationToken.None);

        Assert.True(resultado.SessaoEncontrada);
        Assert.False(sessionManager.TryGet("3000000004", out _));
    }

    [Fact]
    public async Task ReconectarAsync_sem_sessao_ativa_deve_informar_que_nao_encontrou()
    {
        var centralId = await CriarCentralAsync("3000000005");

        using var scope = _provider.CreateScope();
        var resultado = await ObterService(scope).ReconectarAsync(centralId, CancellationToken.None);

        Assert.False(resultado.SessaoEncontrada);
    }

    [Fact]
    public async Task ObterDiagnosticoAsync_com_central_sem_numero_de_serie_deve_sinalizar_pendencia()
    {
        var centralId = await CriarCentralAsync(numeroSerie: null);

        using var scope = _provider.CreateScope();
        var diagnostico = await ObterService(scope).ObterDiagnosticoAsync(centralId, CancellationToken.None);

        var itemNumeroSerie = diagnostico.Itens.Single(i => i.Descricao == "Número de Série cadastrado");
        Assert.False(itemNumeroSerie.Ok);
    }

    [Fact]
    public async Task ObterLogAsync_apos_comando_PGM_deve_conter_entrada_com_Seq()
    {
        var centralId = await CriarCentralAsync("3000000006");

        await using var simulador = new SimuladorActive100Bus("3000000006");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        var pgmService = _provider.GetRequiredService<PgmCommandService>();
        await pgmService.AcionarAsync("3000000006", 1, CancellationToken.None);

        using var scope = _provider.CreateScope();
        var log = await ObterService(scope).ObterLogAsync(centralId, max: 50, CancellationToken.None);

        Assert.Contains(log, e => e.Seq is not null);
    }
}
