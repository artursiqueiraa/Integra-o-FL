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
/// Valida <see cref="ZoneInhibitService"/> contra um <see cref="JflTcpServer"/> real (porta
/// efêmera) e o Central Simulator — prova que Inibir/Desinibir/Consultar zonas funcionam de
/// ponta a ponta (incluindo o mecanismo de "consultar estado atual antes de enviar o bitmap
/// completo"), sem mocks.
/// </summary>
public class ZoneInhibitServiceTests : IAsyncLifetime
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
        services.AddScoped<ZoneInhibitService>();

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

    private ZoneInhibitService ObterService(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ZoneInhibitService>();

    [Fact]
    public async Task InibirAsync_com_central_offline_deve_lancar_BusinessException_409()
    {
        var centralId = await CriarCentralAsync("6000000001");

        using var scope = _provider.CreateScope();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => ObterService(scope).InibirAsync(centralId, 1, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task InibirAsync_com_simulador_conectado_deve_inibir_a_zona_de_verdade()
    {
        var centralId = await CriarCentralAsync("6000000002");

        await using var simulador = new SimuladorActive100Bus("6000000002");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        using var scope = _provider.CreateScope();
        var resultado = await ObterService(scope).InibirAsync(centralId, 5, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.Inibida);
        Assert.Equal(0x01, simulador.Estado.Zonas[4]); // Inibida
    }

    [Fact]
    public async Task DesinibirAsync_apos_inibir_deve_confirmar_nao_inibida_de_verdade()
    {
        var centralId = await CriarCentralAsync("6000000003");

        await using var simulador = new SimuladorActive100Bus("6000000003");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        using var scope = _provider.CreateScope();
        var service = ObterService(scope);
        await service.InibirAsync(centralId, 8, CancellationToken.None);
        var resultado = await service.DesinibirAsync(centralId, 8, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.False(resultado.Inibida);
    }

    [Fact]
    public async Task InibirAsync_de_uma_zona_nao_deve_afetar_outra_ja_inibida_semantica_de_substituicao()
    {
        var centralId = await CriarCentralAsync("6000000004");

        await using var simulador = new SimuladorActive100Bus("6000000004");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        using var scope = _provider.CreateScope();
        var service = ObterService(scope);
        await service.InibirAsync(centralId, 1, CancellationToken.None);
        var resultado = await service.InibirAsync(centralId, 2, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal(0x01, simulador.Estado.Zonas[0]); // zona 1 continua inibida
        Assert.Equal(0x01, simulador.Estado.Zonas[1]); // zona 2 agora tambem inibida
    }

    [Fact]
    public async Task ObterInibidasAsync_deve_listar_as_zonas_inibidas()
    {
        var centralId = await CriarCentralAsync("6000000005");

        await using var simulador = new SimuladorActive100Bus("6000000005");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        using var scope = _provider.CreateScope();
        var service = ObterService(scope);
        await service.InibirAsync(centralId, 3, CancellationToken.None);
        await service.InibirAsync(centralId, 9, CancellationToken.None);

        var inibidas = await service.ObterInibidasAsync(centralId, CancellationToken.None);

        Assert.Contains(3, inibidas);
        Assert.Contains(9, inibidas);
        Assert.Equal(2, inibidas.Count);
    }

    [Fact]
    public async Task InibirAsync_com_zona_invalida_deve_lancar_BusinessException_400()
    {
        var centralId = await CriarCentralAsync("6000000006");
        await using var simulador = new SimuladorActive100Bus("6000000006");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        using var scope = _provider.CreateScope();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => ObterService(scope).InibirAsync(centralId, 100, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }
}
