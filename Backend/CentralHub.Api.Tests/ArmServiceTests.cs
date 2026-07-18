using CentralHub.Api.Data;
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
/// Valida <see cref="ArmService"/> contra um <see cref="JflTcpServer"/> real (porta efêmera) e o
/// Central Simulator — prova que Armar/Desarmar/Stay/Away (incluindo o caso especial do
/// eletrificador, partição 99) funcionam de ponta a ponta, sem mocks.
/// </summary>
public class ArmServiceTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private JflTcpServer _server = null!;
    private int _buildingId;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddJflServer(options => options.Porta = 0);
        services.AddScoped<ArmService>();

        _provider = services.BuildServiceProvider();

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
        await _provider.DisposeAsync();
    }

    private async Task<int> CriarCentralAsync(string numeroSerie)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var central = new Central { Nome = "Central Teste", BuildingId = _buildingId, NumeroSerie = numeroSerie };
        context.Centrals.Add(central);
        await context.SaveChangesAsync();
        return central.Id;
    }

    private ArmService ObterService(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ArmService>();

    [Fact]
    public async Task ArmarAsync_com_central_offline_deve_lancar_BusinessException_409()
    {
        var centralId = await CriarCentralAsync("5000000001");

        using var scope = _provider.CreateScope();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => ObterService(scope).ArmarAsync(centralId, 1, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task ArmarAsync_com_simulador_conectado_deve_armar_a_particao_de_verdade()
    {
        var centralId = await CriarCentralAsync("5000000002");

        await using var simulador = new SimuladorActive100Bus("5000000002");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        using var scope = _provider.CreateScope();
        var resultado = await ObterService(scope).ArmarAsync(centralId, 3, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.EstadoConfirmado);
        Assert.Equal(0x02, simulador.Estado.Particoes[2]); // Armada
    }

    [Fact]
    public async Task DesarmarAsync_apos_armar_deve_confirmar_desarmada_de_verdade()
    {
        var centralId = await CriarCentralAsync("5000000003");

        await using var simulador = new SimuladorActive100Bus("5000000003");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        using var scope = _provider.CreateScope();
        var service = ObterService(scope);
        await service.ArmarAsync(centralId, 1, CancellationToken.None);
        var resultado = await service.DesarmarAsync(centralId, 1, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.False(resultado.EstadoConfirmado);
        Assert.Equal(0x01, simulador.Estado.Particoes[0]); // Desarmada
    }

    [Fact]
    public async Task ArmarStayAsync_com_simulador_deve_confirmar_ArmadaStay_de_verdade()
    {
        var centralId = await CriarCentralAsync("5000000004");

        await using var simulador = new SimuladorActive100Bus("5000000004");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        using var scope = _provider.CreateScope();
        var resultado = await ObterService(scope).ArmarStayAsync(centralId, 2, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal(0x03, simulador.Estado.Particoes[1]); // ArmadaStay
    }

    [Fact]
    public async Task ArmarAsync_com_particao_99_deve_operar_o_eletrificador_de_verdade()
    {
        var centralId = await CriarCentralAsync("5000000005");

        await using var simulador = new SimuladorActive100Bus("5000000005");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        using var scope = _provider.CreateScope();
        var resultado = await ObterService(scope).ArmarAsync(centralId, 99, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal(0x02, simulador.Estado.Eletrificador); // Armado
    }

    [Fact]
    public async Task ArmarAsync_com_numero_de_particao_invalido_deve_lancar_BusinessException_400()
    {
        var centralId = await CriarCentralAsync("5000000006");
        await using var simulador = new SimuladorActive100Bus("5000000006");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        using var scope = _provider.CreateScope();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => ObterService(scope).ArmarAsync(centralId, 50, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task ArmarAsync_com_central_sem_numero_de_serie_deve_lancar_BusinessException_409()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var central = new Central { Nome = "Sem NS", BuildingId = _buildingId, NumeroSerie = null };
        context.Centrals.Add(central);
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => ObterService(scope).ArmarAsync(central.Id, 1, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }
}
