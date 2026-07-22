using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CentralHub.Api.Models;

/// <summary>
/// Cadastro de uma Zona específica de uma Central, com nome/tipo amigáveis para o operador —
/// pura metadata de apresentação. O número (1-99) e o estado ao vivo continuam vindo sempre de
/// <c>GET /api/centrais/{id}/status</c> (fonte de verdade real); este cadastro nunca substitui
/// nem duplica isso, só nomeia o que já existe.
/// </summary>
public class ZonaPredio
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

    /// <summary>Número da zona na central (1 a 99, seção 4.10 do protocolo).</summary>
    [Range(1, 99)]
    public int Numero { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Categoria livre para agrupar/exibir na UI (ex.: "Porta", "Janela", "Movimento", "Perimetral").</summary>
    [MaxLength(50)]
    public string? Tipo { get; set; }

    public bool Ativa { get; set; } = true;
}
