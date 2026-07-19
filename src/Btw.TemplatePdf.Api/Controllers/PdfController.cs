using Btw.TemplatePdf.Application.Pdf;
using Btw.TemplatePdf.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Btw.TemplatePdf.Api.Controllers;

[ApiController]
[Route("api/v1/pdf")]
public sealed class PdfController : ControllerBase
{
    private readonly GeneratePdfByCufeUseCase _generatePdf;
    private readonly GetInvoiceTemplateBindingUseCase _getBinding;

    public PdfController(
        GeneratePdfByCufeUseCase generatePdf,
        GetInvoiceTemplateBindingUseCase getBinding)
    {
        _generatePdf = generatePdf;
        _getBinding = getBinding;
    }

    public sealed record ByCufeBody(string Nit, string Cufe, string? DocumentType);

    [HttpPost("by-cufe")]
    [ProducesResponseType(typeof(GeneratePdfByCufeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GeneratePdfByCufeResponse>> ByCufe(
        [FromBody] ByCufeBody body,
        CancellationToken cancellationToken)
    {
        var documentType = ParseDocumentType(body.DocumentType);
        var result = await _generatePdf.ExecuteAsync(
            new GeneratePdfByCufeRequest(body.Nit, body.Cufe, documentType),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/pdf/bindings/by-cufe?nit=…&amp;cufe=…
    /// Returns whether this invoice was already rendered (invoice_template_bindings).
    /// </summary>
    [HttpGet("bindings/by-cufe")]
    [ProducesResponseType(typeof(GetInvoiceTemplateBindingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GetInvoiceTemplateBindingResponse>> BindingByCufe(
        [FromQuery] string nit,
        [FromQuery] string cufe,
        CancellationToken cancellationToken)
    {
        var result = await _getBinding.ExecuteAsync(
            new GetInvoiceTemplateBindingRequest(nit, cufe),
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
