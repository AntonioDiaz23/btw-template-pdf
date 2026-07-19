using Btw.TemplatePdf.Application.Templates;
using Btw.TemplatePdf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Btw.TemplatePdf.Infrastructure.Templates;

public sealed partial class PostgresTemplateCatalog
{
    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Template '{id}' was not found.");

        if (TemplateStatuses.IsArchived(template.Status))
            return;

        foreach (var version in template.Versions)
        {
            if (VersionStatuses.IsPublished(version.Status) || version.IsPublished)
            {
                version.Status = VersionStatuses.Used;
                version.IsPublished = false;
            }
        }

        template.Status = TemplateStatuses.Archived;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Template '{id}' was not found.");

        if (TemplateStatuses.IsArchived(template.Status))
        {
            throw new InvalidOperationException(
                "La plantilla está archivada. No se puede eliminar: conserva versiones para facturas ya graficadas.");
        }

        var hasReleasedVersion = template.Versions.Any(v =>
            VersionStatuses.IsPublished(v.Status)
            || VersionStatuses.IsUsed(v.Status)
            || v.IsPublished);
        if (hasReleasedVersion)
        {
            throw new InvalidOperationException(
                "La plantilla ya fue publicada. Archívala para ocultarla sin romper facturas pineadas.");
        }

        var bindingCount = await _db.InvoiceTemplateBindings
            .CountAsync(b => b.TemplateId == id, cancellationToken)
            .ConfigureAwait(false);
        if (bindingCount > 0)
        {
            throw new InvalidOperationException(
                "Hay facturas vinculadas a esta plantilla. Archívala en lugar de eliminarla.");
        }

        _db.TemplateVersions.RemoveRange(template.Versions);
        _db.Templates.Remove(template);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TemplateVersionDto> RollbackToVersionAsync(
        Guid id,
        int versionNumber,
        CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Template '{id}' was not found.");

        EnsureNotArchived(template);

        var target = template.Versions.FirstOrDefault(v => v.VersionNumber == versionNumber)
            ?? throw new InvalidOperationException($"Version {versionNumber} was not found.");

        if (VersionStatuses.IsPublished(target.Status) || target.IsPublished)
        {
            var alreadyLiveAt = DateTimeOffset.UtcNow;
            DatabaseInitializer.SyncTemplateFlags(template);
            await DemoteSiblingPublishedTemplatesAsync(template, alreadyLiveAt, cancellationToken)
                .ConfigureAwait(false);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return MapVersion(target);
        }

        if (!VersionStatuses.IsUsed(target.Status))
        {
            throw new InvalidOperationException(
                "Solo se puede volver a una versión usada (ya publicada antes).");
        }

        var tip = Tip(template);
        if (VersionStatuses.IsDraft(tip.Status))
        {
            if (template.Versions.Count <= 1)
                throw new InvalidOperationException("No se puede descartar el único borrador en rollback.");
            template.Versions.Remove(tip);
            _db.TemplateVersions.Remove(tip);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var version in template.Versions)
        {
            if (VersionStatuses.IsPublished(version.Status) || version.IsPublished)
            {
                version.Status = VersionStatuses.Used;
                version.IsPublished = false;
            }
        }

        target.Status = VersionStatuses.Published;
        target.IsPublished = true;
        template.UpdatedAt = now;
        DatabaseInitializer.SyncTemplateFlags(template);
        await DemoteSiblingPublishedTemplatesAsync(template, now, cancellationToken)
            .ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapVersion(target);
    }
}
