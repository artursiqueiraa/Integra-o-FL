using CentralHub.Api.Data;
using CentralHub.Api.DTOs;
using CentralHub.Api.Models;
using CentralHub.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CentralHub.Api.Tests;

/// <summary>
/// Valida o CRUD de <see cref="PgmPredioService"/> — puro cadastro (nome/tipo/ícone), nunca fala
/// com uma central de verdade. Usa EF Core InMemory (banco novo por teste), sem SDK/TCP.
/// </summary>
public class PgmPredioServiceTests
{
    private static async Task<(PgmPredioService Service, AppDbContext Context, int BuildingId, int CentralId)> CriarCenarioAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);

        var building = new Building { Nome = "Prédio Teste" };
        context.Buildings.Add(building);
        await context.SaveChangesAsync();

        var central = new Central { Nome = "Central Teste", BuildingId = building.Id, NumeroSerie = "1000000001" };
        context.Centrals.Add(central);
        await context.SaveChangesAsync();

        var service = new PgmPredioService(context, NullLogger<PgmPredioService>.Instance);
        return (service, context, building.Id, central.Id);
    }

    [Fact]
    public async Task CriarAsync_com_dados_validos_deve_cadastrar()
    {
        var (service, _, buildingId, centralId) = await CriarCenarioAsync();

        var criado = await service.CriarAsync(new CreatePgmPredioDto
        {
            BuildingId = buildingId,
            CentralId = centralId,
            Numero = 1,
            Nome = "Portão da Garagem",
            Tipo = "Portão",
            Icone = "portao",
        });

        Assert.True(criado.Id > 0);
        Assert.Equal("Portão da Garagem", criado.Nome);
        Assert.Equal("Central Teste", criado.CentralNome);
        Assert.True(criado.Ativa);
    }

    [Fact]
    public async Task CriarAsync_com_central_de_outro_predio_deve_lancar_BusinessException_400()
    {
        var (service, context, _, centralId) = await CriarCenarioAsync();
        var outroBuilding = new Building { Nome = "Outro Prédio" };
        context.Buildings.Add(outroBuilding);
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CriarAsync(new CreatePgmPredioDto
        {
            BuildingId = outroBuilding.Id,
            CentralId = centralId,
            Numero = 1,
            Nome = "PGM X",
        }));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CriarAsync_com_central_inexistente_deve_lancar_BusinessException_404()
    {
        var (service, _, buildingId, _) = await CriarCenarioAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CriarAsync(new CreatePgmPredioDto
        {
            BuildingId = buildingId,
            CentralId = 99999,
            Numero = 1,
            Nome = "PGM X",
        }));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task CriarAsync_com_numero_duplicado_na_mesma_central_deve_lancar_BusinessException_409()
    {
        var (service, _, buildingId, centralId) = await CriarCenarioAsync();
        await service.CriarAsync(new CreatePgmPredioDto { BuildingId = buildingId, CentralId = centralId, Numero = 5, Nome = "Luz" });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CriarAsync(
            new CreatePgmPredioDto { BuildingId = buildingId, CentralId = centralId, Numero = 5, Nome = "Outra luz" }));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task ListarAsync_filtrado_por_central_deve_retornar_so_as_dela()
    {
        var (service, context, buildingId, centralId) = await CriarCenarioAsync();
        var outraCentral = new Central { Nome = "Central 2", BuildingId = buildingId, NumeroSerie = "1000000002" };
        context.Centrals.Add(outraCentral);
        await context.SaveChangesAsync();

        await service.CriarAsync(new CreatePgmPredioDto { BuildingId = buildingId, CentralId = centralId, Numero = 1, Nome = "A" });
        await service.CriarAsync(new CreatePgmPredioDto { BuildingId = buildingId, CentralId = outraCentral.Id, Numero = 1, Nome = "B" });

        var resultado = await service.ListarAsync(buildingId: null, centralId: centralId);

        Assert.Single(resultado);
        Assert.Equal("A", resultado.First().Nome);
    }

    [Fact]
    public async Task AtualizarAsync_deve_alterar_nome_tipo_icone_ativa()
    {
        var (service, _, buildingId, centralId) = await CriarCenarioAsync();
        var criado = await service.CriarAsync(new CreatePgmPredioDto { BuildingId = buildingId, CentralId = centralId, Numero = 1, Nome = "Original" });

        var atualizou = await service.AtualizarAsync(criado.Id, new UpdatePgmPredioDto { Nome = "Novo Nome", Tipo = "Sirene", Icone = "sirene", Ativa = false });
        var obtido = await service.ObterPorIdAsync(criado.Id);

        Assert.True(atualizou);
        Assert.Equal("Novo Nome", obtido!.Nome);
        Assert.Equal("Sirene", obtido.Tipo);
        Assert.False(obtido.Ativa);
    }

    [Fact]
    public async Task ExcluirAsync_deve_remover_o_cadastro()
    {
        var (service, _, buildingId, centralId) = await CriarCenarioAsync();
        var criado = await service.CriarAsync(new CreatePgmPredioDto { BuildingId = buildingId, CentralId = centralId, Numero = 1, Nome = "A" });

        var excluiu = await service.ExcluirAsync(criado.Id);
        var obtido = await service.ObterPorIdAsync(criado.Id);

        Assert.True(excluiu);
        Assert.Null(obtido);
    }

    [Fact]
    public async Task ExcluirAsync_de_id_inexistente_deve_retornar_false()
    {
        var (service, _, _, _) = await CriarCenarioAsync();
        Assert.False(await service.ExcluirAsync(99999));
    }
}
