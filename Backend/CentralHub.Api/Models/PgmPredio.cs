using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CentralHub.Api.Models;

/// <summary>
/// Cadastro de uma PGM específica de uma Central, com nome/tipo/ícone amigáveis para o operador
/// — pura metadata de apresentação. O número (1-16) e o estado ao vivo continuam vindo sempre de
/// <c>GET /api/centrais/{id}/status</c> (fonte de verdade real); este cadastro nunca substitui
/// nem duplica isso, só nomeia o que já existe.
/// </summary>
public class PgmPredio
{
    public int Id { get; set; }

    [Required]
    public int BuildingId { get; set; }

    [ForeignKey(nameof(BuildingId))]
    public Building? Building { get; set; }

    [Required]
    public int CentralId { get; set; }

    [ForeignKey(nameof(CentralId))]
    public Central? Central { get; set; }

    /// <summary>Número da PGM na central (1 a 16, seção 4.4/4.5 do protocolo).</summary>
    [Range(1, 16)]
    public int Numero { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Categoria livre para agrupar/exibir na UI (ex.: "Portão", "Luz", "Sirene", "Genérico").</summary>
    [MaxLength(50)]
    public string? Tipo { get; set; }

    /// <summary>Chave de ícone (mapeada para um ícone real no Frontend, ex.: "portao", "luz", "sirene").</summary>
    [MaxLength(50)]
    public string? Icone { get; set; }

    public bool Ativa { get; set; } = true;
}
