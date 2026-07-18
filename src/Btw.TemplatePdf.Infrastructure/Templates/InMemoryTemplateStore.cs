using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Common;
using Btw.TemplatePdf.Domain.Templates;
using System.Text.Json;

namespace Btw.TemplatePdf.Infrastructure.Templates;

public sealed class InMemoryTemplateStore : ITemplateStore
{
    private readonly Dictionary<(string Nit, DocumentType Type), TemplateDefinition> _store = new();

    public InMemoryTemplateStore()
    {
        var demo = CreateDemoTemplate("900000000", DocumentType.Factura);
        _store[(demo.Nit, demo.DocumentType)] = demo;
    }

    public Task<TemplateDefinition?> GetPublishedAsync(
        string nit,
        DocumentType documentType,
        CancellationToken cancellationToken = default)
    {
        _store.TryGetValue((nit, documentType), out var template);
        if (template is null || template.Status != TemplateStatus.Published)
            return Task.FromResult<TemplateDefinition?>(null);
        return Task.FromResult<TemplateDefinition?>(template);
    }

    public Task<TemplateDefinition?> GetByVersionAsync(
        Guid templateId,
        int versionNumber,
        CancellationToken cancellationToken = default)
    {
        var match = _store.Values.FirstOrDefault(t =>
            t.TemplateId == templateId && t.Version == versionNumber);
        return Task.FromResult(match);
    }

    private static TemplateDefinition CreateDemoTemplate(string nit, DocumentType type)
    {
        var blocks = new[]
        {
            new Dictionary<string, object?>
            {
                ["id"] = Guid.NewGuid().ToString(),
                ["type"] = "datos",
                ["props"] = new Dictionary<string, object?>
                {
                    ["panelName"] = "Documento",
                    ["fieldsJson"] = JsonSerializer.Serialize(new[]
                    {
                        new { label = "Número", mode = "campo", value = "documento.numero", format = "ninguno" },
                        new { label = "CUFE", mode = "campo", value = "documento.cufe", format = "ninguno" }
                    })
                }
            }
        };

        return new TemplateDefinition
        {
            TemplateId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Nit = nit,
            DocumentType = type,
            Version = 1,
            Status = TemplateStatus.Published,
            Page = new PageSettings(),
            Features = new TemplateFeatures { SectorSalud = false },
            BlocksJson = JsonSerializer.Serialize(blocks),
            Assets = Array.Empty<TemplateAssetRef>(),
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
