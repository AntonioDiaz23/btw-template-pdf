using Btw.TemplatePdf.Application.Common;
using Btw.TemplatePdf.Application.Pdf;
using Btw.TemplatePdf.Application.Templates;
using Btw.TemplatePdf.Application.Ubl;
using FluentValidation;

namespace Btw.TemplatePdf.Application;

public static class ValidationExtensions
{
    public static async Task ValidateAndThrowAppAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken).ConfigureAwait(false);
        if (result.IsValid)
            return;

        var message = string.Join(" ", result.Errors.Select(e => e.ErrorMessage));
        throw new AppException(AppErrorCodes.ValidationError, message);
    }
}

public sealed class GeneratePdfByCufeRequestValidator : AbstractValidator<GeneratePdfByCufeRequest>
{
    public GeneratePdfByCufeRequestValidator()
    {
        RuleFor(x => x.Nit).NotEmpty().WithMessage("nit is required.");
        RuleFor(x => x.Cufe).NotEmpty().WithMessage("cufe is required.");

        When(x => x.ReplaceBinding, () =>
        {
            RuleFor(x => x.TemplateId)
                .Must(id => id is { } g && g != Guid.Empty)
                .WithMessage("templateId is required when replaceBinding is true.");
        });
    }
}

public sealed class CreateTemplateRequestValidator : AbstractValidator<CreateTemplateRequest>
{
    public CreateTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
        RuleFor(x => x.DocumentType).NotEmpty().WithMessage("documentType is required.");
        RuleFor(x => x.Nit).NotEmpty().WithMessage("nit is required.");
    }
}

public sealed class SaveDraftRequestValidator : AbstractValidator<SaveDraftRequest>
{
    public SaveDraftRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s)
                || s.Trim().Equals("draft", StringComparison.OrdinalIgnoreCase)
                || s.Trim().Equals("published", StringComparison.OrdinalIgnoreCase))
            .WithMessage("status must be 'draft' or 'published'.");

        When(x => IsDraft(x.Status), () =>
        {
            RuleFor(x => x.Html).NotNull().WithMessage("Html is required.");
            RuleFor(x => x.Css).NotNull().WithMessage("Css is required.");
            RuleFor(x => x.SchemaJson).NotNull().WithMessage("SchemaJson is required.");
            RuleFor(x => x.SampleDataJson).NotNull().WithMessage("SampleDataJson is required.");
            RuleFor(x => x.BlocksJson).NotNull().WithMessage("BlocksJson is required.");
        });
    }

    private static bool IsDraft(string? status) =>
        string.IsNullOrWhiteSpace(status)
        || status.Trim().Equals("draft", StringComparison.OrdinalIgnoreCase);
}

public sealed class GetUblByCufeRequestValidator : AbstractValidator<GetUblByCufeRequest>
{
    public GetUblByCufeRequestValidator()
    {
        RuleFor(x => x.Cufe).NotEmpty().WithMessage("cufe is required.");
    }
}

public sealed class GetInvoiceTemplateBindingRequestValidator
    : AbstractValidator<GetInvoiceTemplateBindingRequest>
{
    public GetInvoiceTemplateBindingRequestValidator()
    {
        RuleFor(x => x.Nit).NotEmpty().WithMessage("nit is required.");
        RuleFor(x => x.Cufe).NotEmpty().WithMessage("cufe is required.");
    }
}
