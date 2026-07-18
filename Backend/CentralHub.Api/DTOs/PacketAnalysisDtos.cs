using System.ComponentModel.DataAnnotations;

namespace CentralHub.Api.DTOs;

/// <summary>Corpo do pedido de análise de um pacote colado em hex — ferramenta de desenvolvimento (Fase 0.2).</summary>
public class AnalisarPacoteDto
{
    [Required]
    public string Hex { get; set; } = string.Empty;
}

public class CampoAnalisadoDto
{
    public required string Nome { get; init; }

    public required int Offset { get; init; }

    public required int Tamanho { get; init; }

    public required string ValorBrutoHex { get; init; }

    public required string ValorInterpretado { get; init; }

    public string? Descricao { get; init; }
}

public class PacoteAnalisadoDto
{
    public required bool CabecalhoValido { get; init; }

    public byte? Cab { get; init; }

    public int? Qde { get; init; }

    public byte? Seq { get; init; }

    public byte? Cmd { get; init; }

    public string? CmdNome { get; init; }

    public bool? ChecksumValido { get; init; }

    public required IReadOnlyList<CampoAnalisadoDto> Campos { get; init; }

    public required IReadOnlyList<string> Avisos { get; init; }
}
