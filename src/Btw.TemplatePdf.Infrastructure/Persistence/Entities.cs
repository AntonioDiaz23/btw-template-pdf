namespace Btw.TemplatePdf.Infrastructure.Persistence;

public sealed class TemplateEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "factura";
    /// <summary>draft | published | archived</summary>
    public string Status { get; set; } = "draft";
    public int CurrentVersionNumber { get; set; } = 1;
    /// <summary>Company NIT used for published PDF lookup.</summary>
    public string Nit { get; set; } = "900000000";
    public bool SectorSalud { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<TemplateVersionEntity> Versions { get; set; } = new();
}

public sealed class TemplateVersionEntity
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public TemplateEntity Template { get; set; } = null!;
    public int VersionNumber { get; set; }
    public string Html { get; set; } = string.Empty;
    public string Css { get; set; } = string.Empty;
    public string SchemaJson { get; set; } = "{}";
    public string SampleDataJson { get; set; } = "{}";
    public string BlocksJson { get; set; } = "[]";
    public string PageJson { get; set; } = "{}";
    /// <summary>JSON array of embedded studio assets ({ id, name, mime, dataUrl }).</summary>
    public string AssetsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>draft | published | used</summary>
    public string Status { get; set; } = "draft";
    /// <summary>True when <see cref="Status"/> is published (kept for queries / PDF).</summary>
    public bool IsPublished { get; set; }
}

/// <summary>
/// Pins the first PDF render of a CUFE to a concrete template version.
/// </summary>
public sealed class InvoiceTemplateBindingEntity
{
    public Guid Id { get; set; }
    public string Nit { get; set; } = string.Empty;
    public string Cufe { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "factura";
    public Guid TemplateId { get; set; }
    public int TemplateVersionNumber { get; set; }
    public DateTimeOffset BoundAt { get; set; }
}

/// <summary>Company branding library images (uploaded once; referenced by templates).</summary>
public sealed class BrandAssetEntity
{
    public Guid Id { get; set; }
    public string Nit { get; set; } = "900000000";
    public string Name { get; set; } = string.Empty;
    public string Mime { get; set; } = "image/png";
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public DateTimeOffset CreatedAt { get; set; }
}
