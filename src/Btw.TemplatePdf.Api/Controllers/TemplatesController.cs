using Btw.TemplatePdf.Infrastructure.Templates;
using Microsoft.AspNetCore.Mvc;

namespace Btw.TemplatePdf.Api.Controllers;

[ApiController]
[Route("api/v1/templates")]
public sealed class TemplatesController : ControllerBase
{
    private readonly TemplateCatalogService _catalog;

    public TemplatesController(TemplateCatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TemplateDto>>> List(CancellationToken ct)
    {
        var items = await _catalog.ListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TemplateBundleDto>> Get(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _catalog.GetBundleAsync(id, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "template_not_found", message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<TemplateDto>> Create(
        [FromBody] CreateTemplateRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { code = "validation_error", message = "Name is required." });

        var created = await _catalog.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}/draft")]
    public async Task<ActionResult<TemplateVersionDto>> SaveDraft(
        Guid id,
        [FromBody] SaveDraftRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _catalog.SaveDraftAsync(id, request, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "template_not_found", message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<TemplateVersionDto>> Publish(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _catalog.PublishAsync(id, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "template_not_found", message = ex.Message });
        }
    }
}
