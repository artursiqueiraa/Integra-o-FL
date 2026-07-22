using System.ComponentModel.DataAnnotations;

namespace CentralHub.Api.DTOs;

/// <summary>Dados de entrada para cadastrar/atualizar uma Zona de uma Central.</summary>
public class CreateZonaPredioDto
{
    [Required]
    public int BuildingId { get; set; }

    [Required]
    public int CentralId { get; set; }

    /// <summary>Número da zona na central (1 a 99).</summary>
    [Range(1, 99)]
    public int Numero { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Tipo { get; set; }

    public bool Ativa { get; set; } = true;
}

/// <summary>Dados de entrada para atualização de uma Zona cadastrada.</summary>
public class UpdateZonaPredioDto
{
    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Tipo { get; set; }

    public bool Ativa { get; set; } = true;
}

/// <summary>Dados de saída de uma Zona cadastrada.</summary>
public class ZonaPredioDto
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public string? BuildingNome { get; set; }
    public int CentralId { get; set; }
    public string? CentralNome { get; set; }
    public int Numero { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Tipo { get; set; }
    public bool Ativa { get; set; }
}
