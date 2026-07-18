namespace CentralHub.Api.DTOs;

/// <summary>Resultado de um comando de Inibir/Desinibir uma zona especifica.</summary>
public class ZoneInhibitResultDto
{
    public int Zona { get; set; }

    public bool Sucesso { get; set; }

    /// <summary>Estado real (inibida ou nao) da zona apos o comando, confirmado pela resposta da central.</summary>
    public bool? Inibida { get; set; }

    public string? Erro { get; set; }
}
