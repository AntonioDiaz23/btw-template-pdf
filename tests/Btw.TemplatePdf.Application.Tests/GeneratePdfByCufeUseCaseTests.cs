using Btw.TemplatePdf.Application.Common;
using Btw.TemplatePdf.Application.Pdf;
using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Common;
using Btw.TemplatePdf.Domain.Invoices;
using Btw.TemplatePdf.Domain.Templates;
using FluentValidation;
using NSubstitute;

namespace Btw.TemplatePdf.Application.Tests;

public sealed class GeneratePdfByCufeUseCaseTests
{
    private readonly ITemplateStore _templates = Substitute.For<ITemplateStore>();
    private readonly IUblStore _ubl = Substitute.For<IUblStore>();
    private readonly IUblToViewModelMapper _mapper = Substitute.For<IUblToViewModelMapper>();
    private readonly IAssetStore _assets = Substitute.For<IAssetStore>();
    private readonly IPdfRenderer _renderer = Substitute.For<IPdfRenderer>();
    private readonly IValidator<GeneratePdfByCufeRequest> _validator = new GeneratePdfByCufeRequestValidator();

    private GeneratePdfByCufeUseCase CreateSut() =>
        new(_templates, _ubl, _mapper, _assets, _renderer, _validator);

    [Fact]
    public async Task ExecuteAsync_WhenValid_ReturnsPdfBase64()
    {
        var template = new TemplateDefinition
        {
            TemplateId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Nit = "900000000",
            DocumentType = DocumentType.Factura,
            Version = 2,
            BlocksJson = "[]",
            Html = "<p>{{documento.numero}}</p>",
            Css = ""
        };
        var invoice = new InvoiceViewModel
        {
            Nit = "900000000",
            Cufe = "ABC",
            Data = new Dictionary<string, object?>()
        };

        _templates.GetPublishedAsync("900000000", DocumentType.Factura, Arg.Any<CancellationToken>())
            .Returns(template);
        _ubl.GetUblXmlAsync("900000000", "ABCDEFGH", Arg.Any<CancellationToken>())
            .Returns("<Invoice/>");
        _mapper.Map("900000000", "ABCDEFGH", "<Invoice/>").Returns(invoice);
        _assets.ResolveAsync(Arg.Any<IEnumerable<TemplateAssetRef>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, byte[]>());
        _renderer.RenderAsync(template, invoice, Arg.Any<IReadOnlyDictionary<string, byte[]>>(), Arg.Any<CancellationToken>())
            .Returns("%PDF-1.4"u8.ToArray());

        var result = await CreateSut().ExecuteAsync(
            new GeneratePdfByCufeRequest("900000000", "ABCDEFGH"));

        Assert.Equal("900000000", result.Nit);
        Assert.Equal(template.TemplateId, result.TemplateId);
        Assert.Equal(2, result.TemplateVersion);
        Assert.False(string.IsNullOrWhiteSpace(result.PdfBase64));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCufeMissing_ThrowsValidationError()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            CreateSut().ExecuteAsync(new GeneratePdfByCufeRequest("900000000", "")));

        Assert.Equal(AppErrorCodes.ValidationError, ex.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateMissing_ThrowsTemplateNotFound()
    {
        _templates.GetPublishedAsync("900000000", DocumentType.Factura, Arg.Any<CancellationToken>())
            .Returns((TemplateDefinition?)null);
        _ubl.GetUblXmlAsync("900000000", "CUFE", Arg.Any<CancellationToken>())
            .Returns("<Invoice/>");

        var ex = await Assert.ThrowsAsync<PdfGenerationException>(() =>
            CreateSut().ExecuteAsync(new GeneratePdfByCufeRequest("900000000", "CUFE")));

        Assert.Equal(AppErrorCodes.TemplateNotFound, ex.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUblMissing_ThrowsInvoiceNotFound()
    {
        _templates.GetPublishedAsync("900000000", DocumentType.Factura, Arg.Any<CancellationToken>())
            .Returns(new TemplateDefinition
            {
                TemplateId = Guid.NewGuid(),
                Nit = "900000000",
                DocumentType = DocumentType.Factura,
                BlocksJson = "[]"
            });
        _ubl.GetUblXmlAsync("900000000", "CUFE", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var ex = await Assert.ThrowsAsync<PdfGenerationException>(() =>
            CreateSut().ExecuteAsync(new GeneratePdfByCufeRequest("900000000", "CUFE")));

        Assert.Equal(AppErrorCodes.InvoiceNotFound, ex.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMapperFails_ThrowsMappingError()
    {
        _templates.GetPublishedAsync("900000000", DocumentType.Factura, Arg.Any<CancellationToken>())
            .Returns(new TemplateDefinition
            {
                TemplateId = Guid.NewGuid(),
                Nit = "900000000",
                DocumentType = DocumentType.Factura,
                BlocksJson = "[]"
            });
        _ubl.GetUblXmlAsync("900000000", "CUFE", Arg.Any<CancellationToken>())
            .Returns("<Invoice/>");
        _mapper.When(m => m.Map(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("bad ubl"));

        var ex = await Assert.ThrowsAsync<PdfGenerationException>(() =>
            CreateSut().ExecuteAsync(new GeneratePdfByCufeRequest("900000000", "CUFE")));

        Assert.Equal(AppErrorCodes.MappingError, ex.Code);
    }
}
