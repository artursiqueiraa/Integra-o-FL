using CentralHub.Api.DTOs;
using CentralHub.SDK.Jfl.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CentralHub.Api.Controllers.Dev;

/// <summary>
/// Ferramenta de desenvolvimento/homologação (Fase 0.2 do plano de homologação) — decompõe um
/// pacote JFL colado em hex usando o <see cref="PacketAnalyzer"/> do SDK. Não é uma tela
/// operacional do produto; namespace <c>Controllers.Dev</c> deixa isso explícito. Sem
/// autenticação nesta fase (o projeto ainda não tem auth em nenhum endpoint).
/// </summary>
[ApiController]
[Route("api/dev/packet-inspector")]
[Produces("application/json")]
public class PacketInspectorController : ControllerBase
{
    /// <summary>Analisa um pacote JFL 0x7B colado em hex e devolve a decomposição campo a campo.</summary>
    [HttpPost("analisar")]
    [ProducesResponseType(typeof(PacoteAnalisadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<PacoteAnalisadoDto> Analisar(AnalisarPacoteDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var resultado = PacketAnalyzer.AnalisarHex(dto.Hex);

        return Ok(new PacoteAnalisadoDto
        {
            CabecalhoValido = resultado.CabecalhoValido,
            Cab = resultado.Cab,
            Qde = resultado.Qde,
            Seq = resultado.Seq,
            Cmd = resultado.Cmd,
            CmdNome = resultado.CmdNome,
            ChecksumValido = resultado.ChecksumValido,
            Campos = resultado.Campos.Select(c => new CampoAnalisadoDto
            {
                Nome = c.Nome,
                Offset = c.Offset,
                Tamanho = c.Tamanho,
                ValorBrutoHex = c.ValorBrutoHex,
                ValorInterpretado = c.ValorInterpretado,
                Descricao = c.Descricao,
            }).ToList(),
            Avisos = resultado.Avisos,
        });
    }
}
