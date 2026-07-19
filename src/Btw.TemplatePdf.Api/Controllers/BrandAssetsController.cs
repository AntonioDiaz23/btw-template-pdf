using Btw.TemplatePdf.Application.Common;
using Btw.TemplatePdf.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Btw.TemplatePdf.Api.Controllers;

[ApiController]
[Route("api/v1/brand-assets")]
public sealed class BrandAssetsController : ControllerBase
{
    private const long MaxBytes = 1_500_000;
    private const int MaxAssetsPerNit = 5;
    private readonly TemplateDbContext _db;

    public BrandAssetsController(TemplateDbContext db)
    {
        _db = db;
    }

    public sealed record BrandAssetDto(
        Guid Id,
        string Nit,
        string Name,
        string Mime,
        long SizeBytes,
        DateTimeOffset CreatedAt,
        string ContentUrl);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BrandAssetDto>>> List(
        [FromQuery] string? nit = null,
        CancellationToken ct = default)
    {
        var query = _db.BrandAssets.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(nit))
        {
            var digits = new string(nit.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrEmpty(digits))
                query = query.Where(x => x.Nit == digits);
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var items = rows.Select(x => new BrandAssetDto(
                x.Id,
                x.Nit,
                x.Name,
                x.Mime,
                x.Bytes.LongLength,
                x.CreatedAt,
                $"/api/v1/brand-assets/{x.Id}/content"))
            .ToList();

        return Ok(items);
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct = default)
    {
        var asset = await _db.BrandAssets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);
        if (asset is null)
            return NotFound();

        return File(asset.Bytes, asset.Mime, asset.Name);
    }

    public sealed class UploadBrandAssetForm
    {
        public IFormFile File { get; set; } = null!;
        public string? Nit { get; set; }
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxBytes + 64_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBytes + 64_000)]
    public async Task<ActionResult<BrandAssetDto>> Upload(
        [FromForm] UploadBrandAssetForm form,
        CancellationToken ct = default)
    {
        var file = form.File;
        var nit = form.Nit;
        if (file is null || file.Length == 0)
        {
            throw new AppException(AppErrorCodes.ValidationError, "file is required.");
        }

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException(AppErrorCodes.ValidationError, "Only image files are allowed.");
        }

        if (file.Length > MaxBytes)
        {
            throw new AppException(
                AppErrorCodes.ValidationError,
                "Image exceeds 1.5 MB. Use a smaller file.");
        }

        var companyNit = string.IsNullOrWhiteSpace(nit)
            ? "900000000"
            : new string(nit.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(companyNit))
            companyNit = "900000000";

        var count = await _db.BrandAssets
            .CountAsync(x => x.Nit == companyNit, ct)
            .ConfigureAwait(false);
        if (count >= MaxAssetsPerNit)
        {
            throw new AppException(
                AppErrorCodes.ValidationError,
                $"Maximum of {MaxAssetsPerNit} images per company. Delete one to upload another.");
        }

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct).ConfigureAwait(false);
        var bytes = ms.ToArray();

        var entity = new BrandAssetEntity
        {
            Id = Guid.NewGuid(),
            Nit = companyNit,
            Name = string.IsNullOrWhiteSpace(file.FileName) ? "imagen" : Path.GetFileName(file.FileName),
            Mime = string.IsNullOrWhiteSpace(file.ContentType) ? "image/png" : file.ContentType,
            Bytes = bytes,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.BrandAssets.Add(entity);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return CreatedAtAction(
            nameof(Content),
            new { id = entity.Id },
            new BrandAssetDto(
                entity.Id,
                entity.Nit,
                entity.Name,
                entity.Mime,
                entity.Bytes.LongLength,
                entity.CreatedAt,
                $"/api/v1/brand-assets/{entity.Id}/content"));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.BrandAssets.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);
        if (entity is null)
            return NotFound();

        _db.BrandAssets.Remove(entity);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return NoContent();
    }
}
