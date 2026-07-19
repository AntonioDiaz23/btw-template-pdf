using Btw.TemplatePdf.Application.Pdf;
using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Common;
using FluentValidation;
using NSubstitute;

namespace Btw.TemplatePdf.Application.Tests;

public sealed class GetInvoiceTemplateBindingUseCaseTests
{
    private readonly IInvoiceTemplateBindingStore _bindings =
        Substitute.For<IInvoiceTemplateBindingStore>();
    private readonly IValidator<GetInvoiceTemplateBindingRequest> _validator =
        new GetInvoiceTemplateBindingRequestValidator();

    private GetInvoiceTemplateBindingUseCase CreateSut() =>
        new(_bindings, _validator);

    [Fact]
    public async Task ExecuteAsync_WhenMissing_ReturnsExistsFalse()
    {
        _bindings.FindAsync("900000000", "CUFE123", Arg.Any<CancellationToken>())
            .Returns((InvoiceTemplateBinding?)null);

        var result = await CreateSut().ExecuteAsync(
            new GetInvoiceTemplateBindingRequest("900.000.000", "CUFE123"));

        Assert.False(result.Exists);
        Assert.Equal("900000000", result.Nit);
        Assert.Equal("CUFE123", result.Cufe);
        Assert.Null(result.TemplateId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBound_ReturnsExistsTrue()
    {
        var templateId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var boundAt = DateTimeOffset.Parse("2026-07-18T17:29:14.317Z");
        _bindings.FindAsync("900000000", "CUFE123", Arg.Any<CancellationToken>())
            .Returns(new InvoiceTemplateBinding(
                "900000000",
                "CUFE123",
                DocumentType.Factura,
                templateId,
                2,
                boundAt));

        var result = await CreateSut().ExecuteAsync(
            new GetInvoiceTemplateBindingRequest("900000000", " CUFE123 "));

        Assert.True(result.Exists);
        Assert.Equal(templateId, result.TemplateId);
        Assert.Equal(2, result.TemplateVersion);
        Assert.Equal(DocumentType.Factura, result.DocumentType);
        Assert.Equal(boundAt, result.BoundAt);
    }
}
