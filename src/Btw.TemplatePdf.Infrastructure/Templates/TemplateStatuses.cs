namespace Btw.TemplatePdf.Infrastructure.Templates;

/// <summary>Lifecycle of a template row (not a version).</summary>
public static class TemplateStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";
    /// <summary>Had a live publish that was superseded by another template (same NIT + document type).</summary>
    public const string Used = "used";
    public const string Archived = "archived";

    public static bool IsArchived(string? status) =>
        string.Equals(status, Archived, StringComparison.OrdinalIgnoreCase);
}
