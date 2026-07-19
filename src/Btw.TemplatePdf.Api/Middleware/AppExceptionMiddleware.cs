using System.Diagnostics;
using System.Text.Json;
using Btw.TemplatePdf.Application.Common;

namespace Btw.TemplatePdf.Api.Middleware;

public sealed class AppExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<AppExceptionMiddleware> _logger;

    public AppExceptionMiddleware(RequestDelegate next, ILogger<AppExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            await WriteErrorAsync(context, MapStatus(ex.Code), ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "An unexpected error occurred.");
        }
    }

    private static int MapStatus(string code) =>
        code switch
        {
            AppErrorCodes.ValidationError => StatusCodes.Status400BadRequest,
            AppErrorCodes.TemplateNotFound or AppErrorCodes.InvoiceNotFound => StatusCodes.Status404NotFound,
            AppErrorCodes.Conflict => StatusCodes.Status409Conflict,
            AppErrorCodes.MappingError => StatusCodes.Status422UnprocessableEntity,
            AppErrorCodes.DianUpstreamError => StatusCodes.Status502BadGateway,
            AppErrorCodes.RenderError => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };

    private static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string code,
        string message)
    {
        if (context.Response.HasStarted)
            throw new InvalidOperationException("The response has already started.");

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var payload = new { code, message, traceId };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
