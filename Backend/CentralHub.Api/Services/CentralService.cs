using CentralHub.Api.Data;
using CentralHub.Api.DTOs;
using CentralHub.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Services;

public class CentralService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CentralService> _logger;

    public CentralService(AppDbContext context, ILogger<CentralService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private static CentralDto ParaDto(Central c) => new()
    {
        Id = c.Id,
        Nome = c.Nome,
        IP = c.IP,
        Porta = c.Porta,
        Usuario = c.Usuario,
        BuildingId = c.BuildingId,
        BuildingNome = c.Building?.Nome,
        Fabricante = c.Fabricante,
        Modelo = c.Modelo,
        Firmware = c.Firmware,
        Status = c.Status,
        Latencia = c.Latencia,
        NumeroSerie = c.NumeroSerie,
        UltimoKeepAliveEmUtc = c.UltimoKeepAliveEmUtc,
        UltimoIpConectado = c.UltimoIpConectado,
        ConectadoDesdeUtc = c.ConectadoDesdeUtc
        // Senha propositalmente omitida: nunca é retornada pela API.
    };

    public async Task<IEnumerable<CentralDto>> ListarAsync()
    {
        var centrals = await _context.Centrals.Include(c => c.Building).AsNoTracking().ToListAsync();
        return centrals.Select(ParaDto);
    }

    public async Task<CentralDto?> ObterPorIdAsync(int id)
    {
        var central = await _context.Centrals.Include(c => c.Building).AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return central == null ? null : ParaDto(central);
    }

    private async Task ValidarBuildingAsync(int buildingId)
    {
        var existe = await _context.Buildings.AnyAsync(b => b.Id == buildingId);
        if (!existe)
        {
            throw new BusinessException($"Prédio {buildingId} não encontrado.");
        }
    }

    public async Task<CentralDto> CriarAsync(CreateCentralDto dto)
    {
        await ValidarBuildingAsync(dto.BuildingId);

        var central = new Central
        {
            Nome = dto.Nome,
            // IP/Porta/Usuario/Senha sao legado (ver CreateCentralDto) — nao exigidos mais no
            // cadastro; o Central model continua com colunas nao-anulaveis (sem migracao de
            // banco), entao aplicamos um default vazio quando omitidos.
            IP = dto.IP ?? string.Empty,
            Porta = dto.Porta ?? 0,
            Usuario = dto.Usuario ?? string.Empty,
            Senha = dto.Senha ?? string.Empty,
            BuildingId = dto.BuildingId,
            Fabricante = dto.Fabricante,
            Modelo = dto.Modelo,
            Firmware = dto.Firmware,
            Status = dto.Status,
            Latencia = dto.Latencia,
            NumeroSerie = dto.NumeroSerie
        };

        _context.Centrals.Add(central);
        await _context.SaveChangesAsync();

        // Nunca logar a senha: apenas identificadores e dados não sensíveis.
        _logger.LogInformation("Central {CentralId} ({Nome}) criada para o Prédio {BuildingId}", central.Id, central.Nome, central.BuildingId);

        return ParaDto(central);
    }

    public async Task<bool> AtualizarAsync(int id, UpdateCentralDto dto)
    {
        var existente = await _context.Centrals.FindAsync(id);
        if (existente == null)
        {
            return false;
        }

        await ValidarBuildingAsync(dto.BuildingId);

        existente.Nome = dto.Nome;
        existente.IP = dto.IP ?? string.Empty;
        existente.Porta = dto.Porta ?? 0;
        existente.Usuario = dto.Usuario ?? string.Empty;
        existente.Senha = dto.Senha ?? string.Empty;
        existente.BuildingId = dto.BuildingId;
        existente.Fabricante = dto.Fabricante;
        existente.Modelo = dto.Modelo;
        existente.Firmware = dto.Firmware;
        existente.Status = dto.Status;
        existente.Latencia = dto.Latencia;
        existente.NumeroSerie = dto.NumeroSerie;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Central {CentralId} atualizada", id);
        return true;
    }

    public async Task<bool> ExcluirAsync(int id)
    {
        var central = await _context.Centrals.FindAsync(id);
        if (central == null)
        {
            return false;
        }

        _context.Centrals.Remove(central);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Central {CentralId} excluída", id);
        return true;
    }
}
