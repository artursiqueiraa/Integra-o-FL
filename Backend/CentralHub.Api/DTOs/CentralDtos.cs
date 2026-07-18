using System.ComponentModel.DataAnnotations;

namespace CentralHub.Api.DTOs;

/// <summary>Dados de entrada para criação de uma Central.</summary>
public class CreateCentralDto
{
    [Required]
    [MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Legado — usado apenas pelo antigo fluxo de discagem de saída (removido; ver
    /// Documentation/ARQUITETURA_SESSION_MANAGER.md). Não é mais exigido: a arquitetura
    /// real correlaciona a central pelo <see cref="NumeroSerie"/>, nunca por IP/Porta.
    /// Opcional só para não descartar cadastros antigos que ainda tenham esses dados.
    /// </summary>
    [MaxLength(45)]
    public string? IP { get; set; }

    /// <summary>Legado — ver <see cref="IP"/>.</summary>
    [Range(0, 65535)]
    public int? Porta { get; set; }

    /// <summary>Legado — ver <see cref="IP"/>.</summary>
    [MaxLength(100)]
    public string? Usuario { get; set; }

    /// <summary>Legado — ver <see cref="IP"/>.</summary>
    [MaxLength(200)]
    public string? Senha { get; set; }

    [Required]
    public int BuildingId { get; set; }

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
    /// Numero de serie do equipamento (campo NS do comando de conexao do protocolo
    /// JFL), usado para correlacionar a sessao TCP recebida pelo servidor a esta
    /// Central. Opcional: sem ele, uma central que se conectar fica registrada em
    /// CentralSession mas sem vinculo com um cadastro existente.
    /// </summary>
    [MaxLength(10)]
    public string? NumeroSerie { get; set; }
}

/// <summary>Dados de entrada para atualização de uma Central.</summary>
public class UpdateCentralDto : CreateCentralDto
{
}

/// <summary>
/// Dados de saída de uma Central. Nunca inclui a Senha,
/// para evitar exposição de credenciais pela API.
/// </summary>
public class CentralDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>Legado — ver <see cref="CreateCentralDto.IP"/>.</summary>
    public string? IP { get; set; }

    /// <summary>Legado — ver <see cref="CreateCentralDto.IP"/>.</summary>
    public int? Porta { get; set; }

    /// <summary>Legado — ver <see cref="CreateCentralDto.IP"/>.</summary>
    public string? Usuario { get; set; }

    public int BuildingId { get; set; }
    public string? BuildingNome { get; set; }
    public string? Fabricante { get; set; }
    public string? Modelo { get; set; }
    public string? Firmware { get; set; }
    public string? Status { get; set; }
    public long? Latencia { get; set; }
    public string? NumeroSerie { get; set; }

    /// <summary>Horario (UTC) do ultimo keep-alive recebido na sessao ativa desta central, se houver.</summary>
    public DateTime? UltimoKeepAliveEmUtc { get; set; }

    /// <summary>IP remoto observado na ultima conexao TCP real aceita para esta central.</summary>
    public string? UltimoIpConectado { get; set; }

    /// <summary>Horario (UTC) em que a sessao atual foi aberta; nulo quando a central esta offline.</summary>
    public DateTime? ConectadoDesdeUtc { get; set; }
}
