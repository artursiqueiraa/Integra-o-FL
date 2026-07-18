using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CentralHub.Api.Models;

public enum CentralSessionStatus
{
    Conectada,
    Desconectada,
}

/// <summary>
/// Historico de sessoes TCP do servidor JFL: um registro por conexao aceita,
/// atualizado com o horario do ultimo keep-alive enquanto ativa e fechado quando a
/// central desconecta. Persistido pelo JflSessionPersistenceService, que ouve os
/// eventos do SessionManager (SDK).
/// </summary>
public class CentralSession
{
    public int Id { get; set; }

    [Required]
    [MaxLength(10)]
    public string NumeroSerie { get; set; } = string.Empty;

    /// <summary>Preenchido quando existe uma Central cadastrada com este NumeroSerie; nulo caso contrario.</summary>
    public int? CentralId { get; set; }

    [ForeignKey(nameof(CentralId))]
    public Central? Central { get; set; }

    [MaxLength(15)]
    public string? Imei { get; set; }

    [MaxLength(12)]
    public string? Mac { get; set; }

    public byte Modelo { get; set; }

    [MaxLength(50)]
    public string? ModeloNome { get; set; }

    [MaxLength(20)]
    public string? VersaoFirmware { get; set; }

    [Required]
    [MaxLength(64)]
    public string EnderecoRemoto { get; set; } = string.Empty;

    public CentralSessionStatus Status { get; set; }

    public DateTime ConectadaEmUtc { get; set; }

    public DateTime UltimoKeepAliveEmUtc { get; set; }

    public DateTime? DesconectadaEmUtc { get; set; }
}
