using Btw.TemplatePdf.Application;
using Btw.TemplatePdf.Api.Middleware;
using Btw.TemplatePdf.Infrastructure;
using Btw.TemplatePdf.Infrastructure.Auth;
using Btw.TemplatePdf.Infrastructure.Persistence;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddOpenApi();
builder.Services.AddTemplatePdfApplication();
builder.Services.AddTemplatePdfInfrastructure(builder.Configuration);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Btw.TemplatePdf.Api"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (builder.Environment.IsDevelopment())
            tracing.AddConsoleExporter();
        else
            tracing.AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (builder.Environment.IsDevelopment())
            metrics.AddConsoleExporter();
        else
            metrics.AddOtlpExporter();
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("Studio", policy =>
        policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:4173",
                "http://127.0.0.1:4173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<AppExceptionMiddleware>();
app.UseCors("Studio");

// Capture FE JWT from studio: Authorization: Bearer <token>
app.Use(async (context, next) =>
{
    var accessor = context.RequestServices.GetRequiredService<IFeBearerTokenAccessor>();
    var header = context.Request.Headers.Authorization.ToString();
    if (!string.IsNullOrWhiteSpace(header) &&
        header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        accessor.Token = header["Bearer ".Length..].Trim();
    }

    await next();
});

app.MapControllers();
app.Run();
