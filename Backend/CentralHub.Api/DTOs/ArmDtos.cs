namespace CentralHub.Api.DTOs;

/// <summary>Resultado de um comando de Arme (Armar, Desarmar, Armar Stay ou Armar Away).</summary>
public class ArmCommandResultDto
{
    /// <summary>Numero da particao (1-16), ou 99 quando o comando operou o eletrificador.</summary>
    public int Particao { get; set; }

    public bool Sucesso { get; set; }

    /// <summary>Estado real (armada/armado ou desarmada/desarmado) apos o comando, confirmado pela resposta da central.</summary>
    public bool? EstadoConfirmado { get; set; }

    public string? Erro { get; set; }
}
