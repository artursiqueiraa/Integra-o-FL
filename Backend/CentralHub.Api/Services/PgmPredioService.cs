using CentralHub.Api.Data;
using CentralHub.Api.DTOs;
using CentralHub.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Services;

/// <summary>
/// CRUD do cadastro de PGMs de uma Central (nome/tipo/ícone amigáveis, associados a um número
/// 1-16 real do protocolo). Puro cadastro — nunca fala com uma central, nunca envia comando
/// nenhum; quem faz isso é <see cref="PgmService"/>, reaproveitado sem alteração pela tela de
/// Operação.
/// </summary>
public class PgmPredioService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PgmPredioService> _logger;

    public PgmPredioService(AppDbContext context, ILogger<PgmPredioService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private static PgmPredioDto ParaDto(PgmPredio p) => new()
    {
        Id = p.Id,
        BuildingId = p.BuildingId,
        BuildingNome = p.Building?.Nome,
        CentralId = p.CentralId,
        CentralNome = p.Central?.Nome,
        Numero = p.Numero,
        Nome = p.Nome,
        Tipo = p.Tipo,
        Icone = p.Icone,
        Ativa = p.Ativa,
    };

    public async Task<IEnumerable<PgmPredioDto>> ListarAsync(int? buildingId, int? centralId)
    {
        var query = _context.PgmPredios
            .Include(p => p.Building)
            .Include(p => p.Central)
            .AsNoTracking()
            .AsQueryable();

        if (buildingId.HasValue)
        {
            query = query.Where(p => p.BuildingId == buildingId.Value);
        }

        if (centralId.HasValue)
        {
            query = query.Where(p => p.CentralId == centralId.Value);
        }

        var pgms = await query.OrderBy(p => p.Numero).ToListAsync();
        return pgms.Select(ParaDto);
    }

    public async Task<PgmPredioDto?> ObterPorIdAsync(int id)
    {
        var pgm = await _context.PgmPredios
            .Include(p => p.Building)
            .Include(p => p.Central)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
        return pgm == null ? null : ParaDto(pgm);
    }

    public async Task<PgmPredioDto> CriarAsync(CreatePgmPredioDto dto)
    {
        var central = await ValidarCentralDoPredioAsync(dto.BuildingId, dto.CentralId);

        var jaExiste = await _context.PgmPredios.AnyAsync(p => p.CentralId == dto.CentralId && p.Numero == dto.Numero);
        if (jaExiste)
        {
            throw new BusinessException($"Já existe uma PGM {dto.Numero} cadastrada para esta central.", statusCode: 409);
        }

        var pgm = new PgmPredio
        {
            BuildingId = dto.BuildingId,
            CentralId = dto.CentralId,
            Numero = dto.Numero,
            Nome = dto.Nome,
            Tipo = dto.Tipo,
            Icone = dto.Icone,
            Ativa = dto.Ativa,
        };

        _context.PgmPredios.Add(pgm);
        await _context.SaveChangesAsync();

        _logger.LogInformation("PGM {Numero} ({Nome}) cadastrada para Central {CentralId}", pgm.Numero, pgm.Nome, pgm.CentralId);

        pgm.Central = central;
        return ParaDto(pgm);
    }

    public async Task<bool> AtualizarAsync(int id, UpdatePgmPredioDto dto)
    {
        var existente = await _context.PgmPredios.FindAsync(id);
        if (existente == null)
        {
            return false;
        }

        existente.Nome = dto.Nome;
        existente.Tipo = dto.Tipo;
        existente.Icone = dto.Icone;
        existente.Ativa = dto.Ativa;

        await _context.SaveChangesAsync();
        _logger.LogInformation("PGM {PgmPredioId} atualizada", id);
        return true;
    }

    public async Task<bool> ExcluirAsync(int id)
    {
        var pgm = await _context.PgmPredios.FindAsync(id);
        if (pgm == null)
        {
            return false;
        }

        _context.PgmPredios.Remove(pgm);
        await _context.SaveChangesAsync();
        _logger.LogInformation("PGM {PgmPredioId} excluída", id);
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
