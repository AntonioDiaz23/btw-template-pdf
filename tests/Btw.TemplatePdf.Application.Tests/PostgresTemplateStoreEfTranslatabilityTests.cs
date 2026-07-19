namespace Btw.TemplatePdf.Application.Tests;

/// <summary>
/// Guards against regressing the EF translation bug where
/// <c>VersionStatuses.IsPublished(...)</c> was used inside an IQueryable
/// (InvalidOperationException → 500 on POST /pdf/by-cufe without templateId).
/// </summary>
public sealed class PostgresTemplateStoreEfTranslatabilityTests
{
    [Fact]
    public void GetPublishedAsync_does_not_call_IsPublished_inside_IQueryable()
    {
        var source = File.ReadAllText(ResolveStorePath());

        // IQueryable must only filter by nit + documentType (property equality).
        Assert.Contains(
            ".Where(t => t.Nit == nit && t.DocumentType == docType)",
            source,
            StringComparison.Ordinal);

        // Published selection happens in memory after ToListAsync — never via Any(IsPublished(...)).
        Assert.DoesNotContain(
            "Versions.Any(v => VersionStatuses.IsPublished",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Versions.Any(v => v.IsPublished || VersionStatuses.IsPublished",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "t.Versions.Any(v => VersionStatuses.IsPublished",
            source,
            StringComparison.Ordinal);
    }

    private static string ResolveStorePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "src",
                "Btw.TemplatePdf.Infrastructure",
                "Templates",
                "PostgresTemplateStore.cs");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate PostgresTemplateStore.cs from test base directory.");
    }
}
