using CentralHub.Api.Data;
using CentralHub.Api.DTOs;
using CentralHub.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Services;

/// <summary>
/// CRUD do cadastro de Zonas de uma Central (nome/tipo amigáveis, associados a um número 1-99
/// real do protocolo). Puro cadastro — nunca fala com uma central; quem inibe/desinibe de
/// verdade é <see cref="ZoneInhibitService"/>, reaproveitado sem alteração pela tela de Operação.
/// </summary>
public class ZonaPredioService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ZonaPredioService> _logger;

    public ZonaPredioService(AppDbContext context, ILogger<ZonaPredioService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private static ZonaPredioDto ParaDto(ZonaPredio z) => new()
    {
        Id = z.Id,
        BuildingId = z.BuildingId,
        BuildingNome = z.Building?.Nome,
        CentralId = z.CentralId,
        CentralNome = z.Central?.Nome,
        Numero = z.Numero,
        Nome = z.Nome,
        Tipo = z.Tipo,
        Ativa = z.Ativa,
    };

    public async Task<IEnumerable<ZonaPredioDto>> ListarAsync(int? buildingId, int? centralId)
    {
        var query = _context.ZonaPredios
            .Include(z => z.Building)
            .Include(z => z.Central)
            .AsNoTracking()
            .AsQueryable();

        if (buildingId.HasValue)
        {
            query = query.Where(z => z.BuildingId == buildingId.Value);
        }

        if (centralId.HasValue)
        {
            query = query.Where(z => z.CentralId == centralId.Value);
        }

        var zonas = await query.OrderBy(z => z.Numero).ToListAsync();
        return zonas.Select(ParaDto);
    }

    public async Task<ZonaPredioDto?> ObterPorIdAsync(int id)
    {
        var zona = await _context.ZonaPredios
            .Include(z => z.Building)
            .Include(z => z.Central)
            .AsNoTracking()
            .FirstOrDefaultAsync(z => z.Id == id);
        return zona == null ? null : ParaDto(zona);
    }

    public async Task<ZonaPredioDto> CriarAsync(CreateZonaPredioDto dto)
    {
        var central = await ValidarCentralDoPredioAsync(dto.BuildingId, dto.CentralId);

        var jaExiste = await _context.ZonaPredios.AnyAsync(z => z.CentralId == dto.CentralId && z.Numero == dto.Numero);
        if (jaExiste)
        {
            throw new BusinessException($"Já existe uma Zona {dto.Numero} cadastrada para esta central.", statusCode: 409);
        }

        var zona = new ZonaPredio
        {
            BuildingId = dto.BuildingId,
            CentralId = dto.CentralId,
            Numero = dto.Numero,
            Nome = dto.Nome,
            Tipo = dto.Tipo,
            Ativa = dto.Ativa,
        };

        _context.ZonaPredios.Add(zona);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Zona {Numero} ({Nome}) cadastrada para Central {CentralId}", zona.Numero, zona.Nome, zona.CentralId);

        zona.Central = central;
        return ParaDto(zona);
    }

    public async Task<bool> AtualizarAsync(int id, UpdateZonaPredioDto dto)
    {
        var existente = await _context.ZonaPredios.FindAsync(id);
        if (existente == null)
        {
            return false;
        }

        existente.Nome = dto.Nome;
        existente.Tipo = dto.Tipo;
        existente.Ativa = dto.Ativa;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Zona {ZonaPredioId} atualizada", id);
        return true;
    }

    public async Task<bool> ExcluirAsync(int id)
    {
        var zona = await _context.ZonaPredios.FindAsync(id);
        if (zona == null)
        {
            return false;
        }

        _context.ZonaPredios.Remove(zona);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Zona {ZonaPredioId} excluída", id);
        return true;
    }

    private async Task<Central> ValidarCentralDoPredioAsync(int buildingId, int centralId)
    {
        var central = await _context.Centrals.FirstOrDefaultAsync(c => c.Id == centralId);
        if (central is null)
        {
            throw new BusinessException($"Central {centralId} não encontrada.", statusCode: 404);
        }

        if (central.BuildingId != buildingId)
        {
            throw new BusinessException($"Central {centralId} não pertence ao Prédio {buildingId}.", statusCode: 400);
        }

        return central;
    }
}
