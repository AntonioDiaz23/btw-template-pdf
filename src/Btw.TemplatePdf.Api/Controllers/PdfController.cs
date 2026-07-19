using Btw.TemplatePdf.Application.Pdf;
using Btw.TemplatePdf.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Btw.TemplatePdf.Api.Controllers;

[ApiController]
[Route("api/v1/pdf")]
public sealed class PdfController : ControllerBase
{
    private readonly GeneratePdfByCufeUseCase _generate;
    private readonly GetInvoiceTemplateBindingUseCase _getBinding;

    public PdfController(
        GeneratePdfByCufeUseCase generate,
        GetInvoiceTemplateBindingUseCase getBinding)
    {
        _generate = generate;
        _getBinding = getBinding;
    }

    public sealed record ByCufeBody(string Nit, string Cufe, string? DocumentType);

    /// <summary>
    /// Returns whether a CUFE already has a pinned template version.
    /// Always 200 — <c>exists: false</c> when there is no binding yet.
    /// </summary>
    [HttpGet("bindings/by-cufe")]
    [ProducesResponseType(typeof(GetInvoiceTemplateBindingResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetInvoiceTemplateBindingResponse>> GetBindingByCufe(
        [FromQuery] string nit,
        [FromQuery] string cufe,
        CancellationToken cancellationToken)
    {
        var result = await _getBinding
            .ExecuteAsync(nit, cufe, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("by-cufe")]
    [ProducesResponseType(typeof(GeneratePdfByCufeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GeneratePdfByCufeResponse>> ByCufe(
        [FromBody] ByCufeBody body,
        CancellationToken cancellationToken)
    {
        var documentType = ParseDocumentType(body.DocumentType);
        var result = await _generate.ExecuteAsync(
            new GeneratePdfByCufeRequest(body.Nit, body.Cufe, documentType),
            cancellationToken);
        return Ok(result);
    }

    private static DocumentType ParseDocumentType(string? value) =>
        (value ?? "factura").Trim().ToLowerInvariant() switch
        {
            "nota_credito" => DocumentType.NotaCredito,
            "nota_debito" => DocumentType.NotaDebito,
            "otro" => DocumentType.Otro,
            _ => DocumentType.Factura
        };
}
