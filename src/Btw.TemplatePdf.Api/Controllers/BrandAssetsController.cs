using Btw.TemplatePdf.Application.BrandAssets;
using Microsoft.AspNetCore.Mvc;

namespace Btw.TemplatePdf.Api.Controllers;

[ApiController]
[Route("api/v1/brand-assets")]
public sealed class BrandAssetsController : ControllerBase
{
    private readonly ListBrandAssetsUseCase _list;
    private readonly GetBrandAssetContentUseCase _getContent;
    private readonly UploadBrandAssetUseCase _upload;
    private readonly DeleteBrandAssetUseCase _delete;

    public BrandAssetsController(
        ListBrandAssetsUseCase list,
        GetBrandAssetContentUseCase getContent,
        UploadBrandAssetUseCase upload,
        DeleteBrandAssetUseCase delete)
    {
        _list = list;
        _getContent = getContent;
        _upload = upload;
        _delete = delete;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BrandAssetDto>>> List(
        [FromQuery] string? nit = null,
        CancellationToken ct = default)
    {
        return Ok(await _list.ExecuteAsync(nit, ct).ConfigureAwait(false));
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct = default)
    {
        var asset = await _getContent.ExecuteAsync(id, ct).ConfigureAwait(false);
        return File(asset.Bytes, asset.Mime, asset.Name);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(BrandAssetLimits.MaxBytes + 64_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = BrandAssetLimits.MaxBytes + 64_000)]
    public async Task<ActionResult<BrandAssetDto>> Upload(
        // Do not put [FromForm] on IFormFile — Swashbuckle throws and /swagger/v1/swagger.json returns 500.
        IFormFile file,
        [FromForm] string? nit = null,
        CancellationToken ct = default)
    {
        byte[] bytes = Array.Empty<byte>();
        var fileName = "imagen";
        var contentType = "application/octet-stream";

        if (file is { Length: > 0 })
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct).ConfigureAwait(false);
            bytes = ms.ToArray();
            fileName = file.FileName;
            contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "image/png"
                : file.ContentType;
        }

        var created = await _upload
            .ExecuteAsync(
                new UploadBrandAssetRequest(nit ?? "", fileName, contentType, bytes),
                ct)
            .ConfigureAwait(false);

        return CreatedAtAction(nameof(Content), new { id = created.Id }, created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _delete.ExecuteAsync(id, ct).ConfigureAwait(false);
        return NoContent();
    }
}
