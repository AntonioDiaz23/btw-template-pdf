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

    public TemplatesController(
        ListTemplatesUseCase list,
        GetTemplateUseCase get,
        CreateTemplateUseCase create,
        SaveDraftUseCase saveDraft)
    {
        _list = list;
        _get = get;
        _create = create;
        _saveDraft = saveDraft;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TemplateDto>>> List(CancellationToken ct)
    {
        return Ok(await _list.ExecuteAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TemplateBundleDto>> Get(Guid id, CancellationToken ct)
    {
        return Ok(await _get.ExecuteAsync(id, ct));
    }

    [HttpPost]
    public async Task<ActionResult<TemplateDto>> Create(
        [FromBody] CreateTemplateRequest request,
        CancellationToken ct)
    {
        var created = await _create.ExecuteAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>
    /// Save draft content, or publish when body.status is <c>published</c>
    /// (content fields optional on publish — republishes the current tip).
    /// </summary>
    [HttpPut("{id:guid}/draft")]
    public async Task<ActionResult<TemplateVersionDto>> SaveDraft(
        Guid id,
        [FromBody] SaveDraftRequest request,
        CancellationToken ct)
    {
        return Ok(await _saveDraft.ExecuteAsync(id, request, ct));
    }
}
