using Btw.TemplatePdf.Application.Templates;
using Microsoft.AspNetCore.Mvc;

namespace Btw.TemplatePdf.Api.Controllers;

[ApiController]
[Route("api/v1/templates")]
public sealed class TemplatesController : ControllerBase
{
    private readonly ListTemplatesUseCase _list;
    private readonly GetTemplateUseCase _get;
    private readonly CreateTemplateUseCase _create;
    private readonly SaveDraftUseCase _saveDraft;
    private readonly DeleteDraftUseCase _deleteDraft;
    private readonly ArchiveTemplateUseCase _archive;
    private readonly DeleteTemplateUseCase _delete;
    private readonly RollbackTemplateVersionUseCase _rollback;

    public TemplatesController(
        ListTemplatesUseCase list,
        GetTemplateUseCase get,
        CreateTemplateUseCase create,
        SaveDraftUseCase saveDraft,
        DeleteDraftUseCase deleteDraft,
        ArchiveTemplateUseCase archive,
        DeleteTemplateUseCase delete,
        RollbackTemplateVersionUseCase rollback)
    {
        _list = list;
        _get = get;
        _create = create;
        _saveDraft = saveDraft;
        _deleteDraft = deleteDraft;
        _archive = archive;
        _delete = delete;
        _rollback = rollback;
    }

    /// <summary>List templates for a company NIT.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TemplateDto>>> List(
        [FromQuery] string nit,
        CancellationToken ct)
    {
        return Ok(await _list.ExecuteAsync(nit, ct));
    }

    /// <summary>Get a template bundle scoped to the company NIT.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TemplateBundleDto>> Get(
        Guid id,
        [FromQuery] string nit,
        CancellationToken ct)
    {
        return Ok(await _get.ExecuteAsync(id, nit, ct));
    }

    [HttpPost]
    public async Task<ActionResult<TemplateDto>> Create(
        [FromBody] CreateTemplateRequest request,
        CancellationToken ct)
    {
        var created = await _create.ExecuteAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id, nit = created.Nit }, created);
    }

    /// <summary>
    /// Save draft content, or publish when body.status is <c>published</c>
    /// (content fields optional on publish — promotes the current draft tip).
    /// Requires <c>nit</c> matching the template owner.
    /// </summary>
    [HttpPut("{id:guid}/draft")]
    public async Task<ActionResult<TemplateVersionDto>> SaveDraft(
        Guid id,
        [FromQuery] string nit,
        [FromBody] SaveDraftRequest request,
        CancellationToken ct)
    {
        return Ok(await _saveDraft.ExecuteAsync(id, request, nit, ct));
    }

    /// <summary>Discard the tip draft version (only when tip status is draft).</summary>
    [HttpDelete("{id:guid}/draft")]
    public async Task<IActionResult> DeleteDraft(
        Guid id,
        [FromQuery] string nit,
        CancellationToken ct)
    {
        await _deleteDraft.ExecuteAsync(id, nit, ct);
        return NoContent();
    }

    /// <summary>
    /// Soft-archive: hide from catalog and live PDF selection; keep versions for pinned CUFEs.
    /// </summary>
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(
        Guid id,
        [FromQuery] string nit,
        CancellationToken ct)
    {
        await _archive.ExecuteAsync(id, nit, ct);
        return NoContent();
    }

    /// <summary>
    /// Hard-delete only unused draft templates (never published/used, no invoice bindings).
    /// Otherwise returns 409 — use archive instead.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] string nit,
        CancellationToken ct)
    {
        await _delete.ExecuteAsync(id, nit, ct);
        return NoContent();
    }

    /// <summary>
    /// Rollback: make a previous <c>used</c> version the live published one again
    /// (same version number — does not create v+1). Discards an open draft tip if any.
    /// </summary>
    [HttpPost("{id:guid}/versions/{versionNumber:int}/rollback")]
    public async Task<ActionResult<TemplateVersionDto>> Rollback(
        Guid id,
        int versionNumber,
        [FromQuery] string nit,
        CancellationToken ct)
    {
        return Ok(await _rollback.ExecuteAsync(id, versionNumber, nit, ct));
    }
}
