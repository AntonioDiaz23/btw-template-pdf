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
                    CreatedAt = now,
                    IsPublished = true
                }
            }
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded demo template for NIT 900000000.");
    }
}
