using Btw.TemplatePdf.Infrastructure.Persistence;
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
        await EnsureInvoiceTemplateBindingsTableAsync(db, ct);
        await EnsureBrandAssetsTableAsync(db, ct);
        logger.LogInformation("PostgreSQL schema ensured for TemplatePdf.");

        if (await db.Templates.AnyAsync(ct))
            return;

        var now = DateTimeOffset.UtcNow;
        var templateId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var versionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        db.Templates.Add(new TemplateEntity
        {
            Id = templateId,
            Name = "Demo FE (API)",
            DocumentType = "factura",
            Status = "published",
            CurrentVersionNumber = 1,
            Nit = "900000000",
            SectorSalud = false,
            UpdatedAt = now,
            Versions =
            {
                new TemplateVersionEntity
                {
                    Id = versionId,
                    TemplateId = templateId,
                    VersionNumber = 1,
                    Html = "<div>Demo</div>",
                    Css = "",
                    SchemaJson = "{}",
                    SampleDataJson = "{}",
                    BlocksJson = "[]",
                    PageJson = "{}",
                    AssetsJson = "[]",
                    CreatedAt = now,
                    IsPublished = true
                }
            }
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded demo template for NIT 900000000.");
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
