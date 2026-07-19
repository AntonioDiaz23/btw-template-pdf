using Btw.TemplatePdf.Infrastructure.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Btw.TemplatePdf.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TemplateDbContext>>();

        await db.Database.EnsureCreatedAsync(ct);
        await EnsureAssetsJsonColumnAsync(db, ct);
        await EnsureVersionStatusColumnAsync(db, ct);
        await EnsureInvoiceTemplateBindingsTableAsync(db, ct);
        await EnsureBrandAssetsTableAsync(db, ct);
        logger.LogInformation("PostgreSQL schema ensured for TemplatePdf.");
    }

    private static async Task EnsureAssetsJsonColumnAsync(TemplateDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE template_versions
            ADD COLUMN IF NOT EXISTS "AssetsJson" text NOT NULL DEFAULT '[]';
            """,
            ct);
    }

    private static async Task EnsureVersionStatusColumnAsync(TemplateDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE template_versions
            ADD COLUMN IF NOT EXISTS "Status" character varying(20) NOT NULL DEFAULT 'draft';
            """,
            ct);

        // Backfill: published → published; older unpublished snapshots → used; tip unpublished → draft.
        var templates = await db.Templates
            .Include(t => t.Versions)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var template in templates)
        {
            var published = template.Versions
                .Where(v => v.IsPublished)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault();
            var publishedNum = published?.VersionNumber ?? 0;

            foreach (var version in template.Versions)
            {
                if (version.IsPublished ||
                    string.Equals(version.Status, VersionStatuses.Published, StringComparison.OrdinalIgnoreCase))
                {
                    version.Status = VersionStatuses.Published;
                    version.IsPublished = true;
                }
                else if (publishedNum > 0 && version.VersionNumber < publishedNum)
                {
                    version.Status = VersionStatuses.Used;
                    version.IsPublished = false;
                }
                else
                {
                    version.Status = VersionStatuses.Draft;
                    version.IsPublished = false;
                }
            }

            SyncTemplateFlags(template);
        }

        if (templates.Count > 0)
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    internal static void SyncTemplateFlags(TemplateEntity template)
    {
        var tip = template.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        var published = template.Versions
            .Where(v => VersionStatuses.IsPublished(v.Status) || v.IsPublished)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();
        var hasDraft = tip is not null && VersionStatuses.IsDraft(tip.Status);

        template.CurrentVersionNumber = hasDraft
            ? tip!.VersionNumber
            : published?.VersionNumber ?? tip?.VersionNumber ?? 1;

        // Preserve soft-archive; do not resurrect archived templates via version sync.
        if (TemplateStatuses.IsArchived(template.Status))
            return;

        template.Status = published is null ? "draft" : "published";
    }

    private static async Task EnsureInvoiceTemplateBindingsTableAsync(TemplateDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS invoice_template_bindings (
                "Id" uuid NOT NULL,
                "Nit" character varying(20) NOT NULL,
                "Cufe" character varying(128) NOT NULL,
                "DocumentType" character varying(40) NOT NULL,
                "TemplateId" uuid NOT NULL,
                "TemplateVersionNumber" integer NOT NULL,
                "BoundAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_invoice_template_bindings" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_invoice_template_bindings_Cufe"
                ON invoice_template_bindings ("Cufe");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_invoice_template_bindings_Nit_Cufe"
                ON invoice_template_bindings ("Nit", "Cufe");
            """,
            ct);
    }

    private static async Task EnsureBrandAssetsTableAsync(TemplateDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS brand_assets (
                "Id" uuid NOT NULL,
                "Nit" character varying(20) NOT NULL,
                "Name" character varying(260) NOT NULL,
                "Mime" character varying(120) NOT NULL,
                "Bytes" bytea NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_brand_assets" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_brand_assets_Nit" ON brand_assets ("Nit");
            """,
            ct);
    }
}
