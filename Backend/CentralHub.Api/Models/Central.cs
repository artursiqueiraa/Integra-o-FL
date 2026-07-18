using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CentralHub.Api.Models;

public class Central
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Required]
    [MaxLength(45)]
    public string IP { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Porta { get; set; }

    [Required]
    [MaxLength(100)]
    public string Usuario { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Senha { get; set; } = string.Empty;

    [Required]
    public int BuildingId { get; set; }

    [ForeignKey(nameof(BuildingId))]
    public Building? Building { get; set; }

    [MaxLength(100)]
    public string? Fabricante { get; set; }

    [MaxLength(100)]
    public string? Modelo { get; set; }

    [MaxLength(100)]
    public string? Firmware { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }

    public long? Latencia { get; set; }

    /// <summary>
    /// Numero de serie do equipamento (campo NS do comando de conexao 0x21), usado
    /// para correlacionar uma sessao TCP recebida pelo servidor JFL a esta Central.
    /// Diferente de IP/Porta, e estavel mesmo quando o equipamento esta atras de
    /// NAT/CGNAT ou muda de endereco entre reconexoes.
    /// </summary>
    [MaxLength(10)]
    public string? NumeroSerie { get; set; }

    /// <summary>Horario (UTC) do ultimo keep-alive (0x40) recebido na sessao ativa desta central.</summary>
    public DateTime? UltimoKeepAliveEmUtc { get; set; }

    /// <summary>
    /// Endereco IP remoto observado na ultima conexao TCP aceita para esta central
    /// (vindo da sessao real, nao do campo IP cadastrado manualmente — a central
    /// disca para o CentralHub, entao seu IP de origem pode variar entre conexoes).
    /// </summary>
    [MaxLength(45)]
    public string? UltimoIpConectado { get; set; }

    /// <summary>Horario (UTC) em que a sessao TCP atualmente ativa foi aberta (nulo se offline).</summary>
    public DateTime? ConectadoDesdeUtc { get; set; }
}
