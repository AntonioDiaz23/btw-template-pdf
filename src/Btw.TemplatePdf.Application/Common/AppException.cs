namespace Btw.TemplatePdf.Application.Common;

/// <summary>Typed application failure with a stable machine-readable <see cref="Code"/>.</summary>
public class AppException : Exception
{
    public string Code { get; }

    public AppException(string code, string message) : base(message)
    {
        Code = code;
    }

    public AppException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}

public static class AppErrorCodes
{
    public const string ValidationError = "validation_error";
    public const string TemplateNotFound = "template_not_found";
    public const string InvoiceNotFound = "invoice_not_found";
    public const string MappingError = "mapping_error";
    public const string RenderError = "render_error";
    public const string DianUpstreamError = "dian_upstream_error";
}
