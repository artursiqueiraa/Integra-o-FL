using System.ComponentModel.DataAnnotations;

namespace CentralHub.Api.DTOs;

/// <summary>Dados de entrada para o comando de Pulso (Acionar + aguardar + Desacionar).</summary>
public class PulsoPgmDto
{
    /// <summary>Intervalo entre acionar e desacionar, em milissegundos (100ms a 60s).</summary>
    [Range(100, 60000)]
    public int DuracaoMs { get; set; } = 1000;
}

/// <summary>Resultado de um comando de PGM (Ligar, Desligar ou Pulso).</summary>
public class PgmCommandResultDto
{
    public int Pgm { get; set; }

    public bool Sucesso { get; set; }

    /// <summary>Estado real da PGM apos o comando, confirmado pela resposta da central.</summary>
    public bool? EstadoConfirmado { get; set; }

    public string? Erro { get; set; }
}
