using Btw.TemplatePdf.Application.Common;
using Btw.TemplatePdf.Application.Pdf;
using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Common;
using Btw.TemplatePdf.Domain.Invoices;
using Btw.TemplatePdf.Domain.Templates;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Btw.TemplatePdf.Application.Tests;

public sealed class GeneratePdfByCufeUseCaseTests
{
    private readonly ITemplateStore _templates = Substitute.For<ITemplateStore>();
    private readonly IInvoiceTemplateBindingStore _bindings = Substitute.For<IInvoiceTemplateBindingStore>();
    private readonly IUblStore _ubl = Substitute.For<IUblStore>();
    private readonly IUblToViewModelMapper _mapper = Substitute.For<IUblToViewModelMapper>();
    private readonly IAssetStore _assets = Substitute.For<IAssetStore>();
    private readonly IPdfRenderer _renderer = Substitute.For<IPdfRenderer>();
    private readonly IPdfMetadataWriter _metadata = Substitute.For<IPdfMetadataWriter>();
    private readonly IValidator<GeneratePdfByCufeRequest> _validator = new GeneratePdfByCufeRequestValidator();

    private GeneratePdfByCufeUseCase CreateSut()
    {
        _metadata.Apply(Arg.Any<byte[]>(), Arg.Any<PdfFileMetadata>())
            .Returns(ci => ci.ArgAt<byte[]>(0));
        return new(
            _templates,
            _bindings,
            _ubl,
            _mapper,
            _assets,
            _renderer,
            _metadata,
            _validator,
            NullLogger<GeneratePdfByCufeUseCase>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValid_ReturnsPdfBase64_AndPinsTemplate()
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

        _bindings.FindAsync("900000000", "ABCDEFGH", Arg.Any<CancellationToken>())
            .Returns((InvoiceTemplateBinding?)null);
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
        Assert.False(result.ReusedPinnedTemplate);
        Assert.False(string.IsNullOrWhiteSpace(result.PdfBase64));
        _metadata.Received(1).Apply(
            Arg.Any<byte[]>(),
            Arg.Is<PdfFileMetadata>(m =>
                m.Creator == "BTW Template PDF" &&
                m.Producer == "BTW Template PDF" &&
                m.Author == "NIT 900000000"));
        await _bindings.Received(1).SaveAsync(
            Arg.Is<InvoiceTemplateBinding>(b =>
                b.Cufe == "ABCDEFGH" &&
                b.TemplateId == template.TemplateId &&
                b.TemplateVersionNumber == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPinned_UsesHistoricalVersion_NotLatestPublished()
    {
        var pinnedId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var pinned = new TemplateDefinition
        {
            TemplateId = pinnedId,
            Nit = "900000000",
            DocumentType = DocumentType.Factura,
            Version = 1,
            BlocksJson = "[]",
            Html = "<p>old</p>",
            Css = ""
        };
        var invoice = new InvoiceViewModel
        {
            Nit = "900000000",
            Cufe = "CUFE1",
            Data = new Dictionary<string, object?>()
        };

        _bindings.FindAsync("900000000", "CUFE1", Arg.Any<CancellationToken>())
            .Returns(new InvoiceTemplateBinding(
                "900000000",
                "CUFE1",
                DocumentType.Factura,
                pinnedId,
                1,
                DateTimeOffset.UtcNow));
        _templates.GetByVersionAsync(pinnedId, 1, Arg.Any<CancellationToken>())
            .Returns(pinned);
        _ubl.GetUblXmlAsync("900000000", "CUFE1", Arg.Any<CancellationToken>())
            .Returns("<Invoice/>");
        _mapper.Map("900000000", "CUFE1", "<Invoice/>").Returns(invoice);
        _assets.ResolveAsync(Arg.Any<IEnumerable<TemplateAssetRef>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, byte[]>());
        _renderer.RenderAsync(pinned, invoice, Arg.Any<IReadOnlyDictionary<string, byte[]>>(), Arg.Any<CancellationToken>())
            .Returns("%PDF-1.4"u8.ToArray());

        var result = await CreateSut().ExecuteAsync(
            new GeneratePdfByCufeRequest("900000000", "CUFE1"));

        Assert.True(result.ReusedPinnedTemplate);
        Assert.Equal(1, result.TemplateVersion);
        await _templates.DidNotReceive().GetPublishedAsync(
            Arg.Any<string>(),
            Arg.Any<DocumentType>(),
            Arg.Any<CancellationToken>());
        await _bindings.DidNotReceive().SaveAsync(
            Arg.Any<InvoiceTemplateBinding>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCufeMissing_ThrowsValidationError()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            CreateSut().ExecuteAsync(new GeneratePdfByCufeRequest("900000000", "")));

        Assert.Equal(AppErrorCodes.ValidationError, ex.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReplaceBindingWithoutTemplateId_ThrowsValidationError()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            CreateSut().ExecuteAsync(
                new GeneratePdfByCufeRequest("900000000", "CUFE1", ReplaceBinding: true)));

        Assert.Equal(AppErrorCodes.ValidationError, ex.Code);
        Assert.Contains("templateId", ex.Message, StringComparison.OrdinalIgnoreCase);
        await _bindings.DidNotReceive().FindAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await _templates.DidNotReceive().GetByVersionAsync(
            Arg.Any<Guid>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenReplaceBindingWithEmptyTemplateId_ThrowsValidationError()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            CreateSut().ExecuteAsync(
                new GeneratePdfByCufeRequest(
                    "900000000",
                    "CUFE1",
                    TemplateId: Guid.Empty,
                    ReplaceBinding: true)));

        Assert.Equal(AppErrorCodes.ValidationError, ex.Code);
        Assert.Contains("templateId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateMissing_ThrowsTemplateNotFound()
    {
        _bindings.FindAsync("900000000", "CUFE", Arg.Any<CancellationToken>())
            .Returns((InvoiceTemplateBinding?)null);
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
        _bindings.FindAsync("900000000", "CUFE", Arg.Any<CancellationToken>())
            .Returns((InvoiceTemplateBinding?)null);
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
        _bindings.FindAsync("900000000", "CUFE", Arg.Any<CancellationToken>())
            .Returns((InvoiceTemplateBinding?)null);
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

    [Fact]
    public async Task ExecuteAsync_WhenOverrideWithoutReplace_DoesNotTouchBinding()
    {
        var pinnedId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var otherId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var other = new TemplateDefinition
        {
            TemplateId = otherId,
            Nit = "900000000",
            DocumentType = DocumentType.Factura,
            Version = 3,
            BlocksJson = "[]",
            Html = "<p>new</p>",
            Css = ""
        };
        var invoice = new InvoiceViewModel
        {
            Nit = "900000000",
            Cufe = "CUFE1",
            Data = new Dictionary<string, object?>()
        };

        _bindings.FindAsync("900000000", "CUFE1", Arg.Any<CancellationToken>())
            .Returns(new InvoiceTemplateBinding(
                "900000000",
                "CUFE1",
                DocumentType.Factura,
                pinnedId,
                1,
                DateTimeOffset.UtcNow));
        _templates.GetPublishedByIdAsync(otherId, Arg.Any<CancellationToken>())
            .Returns(other);
        _ubl.GetUblXmlAsync("900000000", "CUFE1", Arg.Any<CancellationToken>())
            .Returns("<Invoice/>");
        _mapper.Map("900000000", "CUFE1", "<Invoice/>").Returns(invoice);
        _assets.ResolveAsync(Arg.Any<IEnumerable<TemplateAssetRef>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, byte[]>());
        _renderer.RenderAsync(other, invoice, Arg.Any<IReadOnlyDictionary<string, byte[]>>(), Arg.Any<CancellationToken>())
            .Returns("%PDF-1.4"u8.ToArray());

        var result = await CreateSut().ExecuteAsync(
            new GeneratePdfByCufeRequest("900000000", "CUFE1", TemplateId: otherId, ReplaceBinding: false));

        Assert.False(result.ReusedPinnedTemplate);
        Assert.False(result.BindingReplaced);
        Assert.Equal(otherId, result.TemplateId);
        await _bindings.DidNotReceive().ReplaceAsync(
            Arg.Any<InvoiceTemplateBinding>(),
            Arg.Any<CancellationToken>());
        await _bindings.DidNotReceive().SaveAsync(
            Arg.Any<InvoiceTemplateBinding>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenOverrideWithReplace_UpdatesBinding_AndUsesPublishedVersion()
    {
        var pinnedId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var otherId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var other = new TemplateDefinition
        {
            TemplateId = otherId,
            Nit = "900000000",
            DocumentType = DocumentType.Factura,
            Version = 3,
            BlocksJson = "[]",
            Html = "<p>new</p>",
            Css = ""
        };
        var invoice = new InvoiceViewModel
        {
            Nit = "900000000",
            Cufe = "CUFE1",
            Data = new Dictionary<string, object?>()
        };

        _bindings.FindAsync("900000000", "CUFE1", Arg.Any<CancellationToken>())
            .Returns(new InvoiceTemplateBinding(
                "900000000",
                "CUFE1",
                DocumentType.Factura,
                pinnedId,
                1,
                DateTimeOffset.UtcNow));
        _templates.GetPublishedByIdAsync(otherId, Arg.Any<CancellationToken>())
            .Returns(other);
        _ubl.GetUblXmlAsync("900000000", "CUFE1", Arg.Any<CancellationToken>())
            .Returns("<Invoice/>");
        _mapper.Map("900000000", "CUFE1", "<Invoice/>").Returns(invoice);
        _assets.ResolveAsync(Arg.Any<IEnumerable<TemplateAssetRef>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, byte[]>());
        _renderer.RenderAsync(other, invoice, Arg.Any<IReadOnlyDictionary<string, byte[]>>(), Arg.Any<CancellationToken>())
            .Returns("%PDF-1.4"u8.ToArray());

        var result = await CreateSut().ExecuteAsync(
            new GeneratePdfByCufeRequest("900000000", "CUFE1", TemplateId: otherId, ReplaceBinding: true));

        Assert.True(result.BindingReplaced);
        Assert.False(result.ReusedPinnedTemplate);
        Assert.Equal(otherId, result.TemplateId);
        Assert.Equal(3, result.TemplateVersion);
        await _templates.Received(1).GetPublishedByIdAsync(otherId, Arg.Any<CancellationToken>());
        await _templates.DidNotReceive().GetByVersionAsync(
            Arg.Any<Guid>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
        await _bindings.Received(1).ReplaceAsync(
            Arg.Is<InvoiceTemplateBinding>(b =>
                b.TemplateId == otherId && b.TemplateVersionNumber == 3),
            Arg.Any<CancellationToken>());
    }
}
