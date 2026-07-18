using Btw.TemplatePdf.Application.Ubl;
using Microsoft.AspNetCore.Mvc;

namespace Btw.TemplatePdf.Api.Controllers;

/// <summary>
/// UBL lookup by CUFE — same FE path as ARService DianDocument/GetUbl
/// (<c>GetDocumentFromDian</c>).
/// </summary>
[ApiController]
[Route("api/v1/ubl")]
public sealed class UblController : ControllerBase
{
    private readonly GetUblByCufeUseCase _useCase;

    public UblController(GetUblByCufeUseCase useCase)
    {
        _useCase = useCase;
    }

    /// <summary>GET /api/v1/ubl/by-cufe?cufe=…&amp;nit=…&amp;typeDocument=UBL</summary>
    [HttpGet("by-cufe")]
    [ProducesResponseType(typeof(GetUblByCufeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetUblByCufeResponse>> ByCufe(
        [FromQuery] string cufe,
        [FromQuery] string? nit = null,
        [FromQuery] string typeDocument = "UBL",
        CancellationToken cancellationToken = default)
    {
        var result = await _useCase.ExecuteAsync(
            new GetUblByCufeRequest(cufe, nit, typeDocument),
            cancellationToken);
        return Ok(result);
    }
}
