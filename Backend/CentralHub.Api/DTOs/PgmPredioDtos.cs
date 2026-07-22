using System.ComponentModel.DataAnnotations;

namespace CentralHub.Api.DTOs;

/// <summary>Dados de entrada para cadastrar/atualizar uma PGM de uma Central.</summary>
public class CreatePgmPredioDto
{
    [Required]
    public int BuildingId { get; set; }

    [Required]
    public int CentralId { get; set; }

    /// <summary>Número da PGM na central (1 a 16).</summary>
    [Range(1, 16)]
    public int Numero { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Tipo { get; set; }

    [MaxLength(50)]
    public string? Icone { get; set; }

    public bool Ativa { get; set; } = true;
}

/// <summary>Dados de entrada para atualização de uma PGM cadastrada.</summary>
public class UpdatePgmPredioDto
{
    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Tipo { get; set; }

    [MaxLength(50)]
    public string? Icone { get; set; }

    public bool Ativa { get; set; } = true;
}

/// <summary>Dados de saída de uma PGM cadastrada.</summary>
public class PgmPredioDto
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public string? BuildingNome { get; set; }
    public int CentralId { get; set; }
    public string? CentralNome { get; set; }
    public int Numero { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Tipo { get; set; }
    public string? Icone { get; set; }
    public bool Ativa { get; set; }
}
