using Btw.TemplatePdf.Application.Common;
using Btw.TemplatePdf.Application.Templates;
using NSubstitute;

namespace Btw.TemplatePdf.Application.Tests;

public sealed class TemplateUseCaseTests
{
    private readonly ITemplateCatalog _catalog = Substitute.For<ITemplateCatalog>();

    [Fact]
    public async Task Create_WhenNameMissing_ThrowsValidationError()
    {
        var sut = new CreateTemplateUseCase(_catalog, new CreateTemplateRequestValidator());

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.ExecuteAsync(new CreateTemplateRequest(
                Name: "",
                DocumentType: "factura",
                Nit: "900000000")));

        Assert.Equal(AppErrorCodes.ValidationError, ex.Code);
    }

    [Fact]
    public async Task Create_WhenNitMissing_ThrowsValidationError()
    {
        var sut = new CreateTemplateUseCase(_catalog, new CreateTemplateRequestValidator());

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.ExecuteAsync(new CreateTemplateRequest(
                Name: "Demo",
                DocumentType: "factura",
                Nit: "")));

        Assert.Equal(AppErrorCodes.ValidationError, ex.Code);
    }

    [Fact]
    public async Task Create_WhenValid_ReturnsCreatedTemplate()
    {
        var request = new CreateTemplateRequest(
            Name: "Demo",
            DocumentType: "factura",
            Nit: "900.000.000-1");
        var expected = new TemplateDto(
            Guid.NewGuid(),
            "Demo",
            "factura",
            "draft",
            1,
            DateTimeOffset.UtcNow,
            "9000000001",
            false);
        _catalog.CreateAsync(
                Arg.Is<CreateTemplateRequest>(r => r.Nit == "9000000001"),
                Arg.Any<CancellationToken>())
            .Returns(expected);

        var sut = new CreateTemplateUseCase(_catalog, new CreateTemplateRequestValidator());
        var result = await sut.ExecuteAsync(request);

        Assert.Equal("Demo", result.Name);
        Assert.Equal("draft", result.Status);
    }

    [Fact]
    public async Task List_WhenNitMissing_ThrowsValidationError()
    {
        var sut = new ListTemplatesUseCase(_catalog);

        var ex = await Assert.ThrowsAsync<AppException>(() => sut.ExecuteAsync(""));

        Assert.Equal(AppErrorCodes.ValidationError, ex.Code);
    }

    [Fact]
    public async Task List_WhenNitProvided_CallsCatalogWithNormalizedNit()
    {
        _catalog.ListAsync("900000000", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TemplateDto>());

        var sut = new ListTemplatesUseCase(_catalog);
        await sut.ExecuteAsync("900.000.000");

        await _catalog.Received(1).ListAsync("900000000", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveDraft_WhenStatusPublishedAndMissing_ThrowsTemplateNotFound()
    {
        var id = Guid.NewGuid();
        _catalog.GetBundleAsync(id, Arg.Any<CancellationToken>()).Returns((TemplateBundleDto?)null);

        var sut = new SaveDraftUseCase(_catalog, new SaveDraftRequestValidator());
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.ExecuteAsync(id, new SaveDraftRequest(Status: "published"), "900000000"));

        Assert.Equal(AppErrorCodes.TemplateNotFound, ex.Code);
    }

    [Fact]
    public async Task SaveDraft_WhenStatusPublished_CallsPublish()
    {
        var id = Guid.NewGuid();
        var bundle = new TemplateBundleDto(
            new TemplateDto(id, "Demo", "factura", "draft", 1, DateTimeOffset.UtcNow, "900000000", false),
            Array.Empty<TemplateVersionDto>());
        var version = new TemplateVersionDto(
            Guid.NewGuid(),
            id,
            1,
            "",
            "",
            "{}",
            "{}",
            "[]",
            DateTimeOffset.UtcNow,
            true);
        _catalog.GetBundleAsync(id, Arg.Any<CancellationToken>()).Returns(bundle);
        _catalog.PublishAsync(id, Arg.Any<CancellationToken>()).Returns(version);

        var sut = new SaveDraftUseCase(_catalog, new SaveDraftRequestValidator());
        var result = await sut.ExecuteAsync(id, new SaveDraftRequest(Status: "published"), "900000000");

        Assert.True(result.IsPublished);
        await _catalog.Received(1).PublishAsync(id, Arg.Any<CancellationToken>());
        await _catalog.DidNotReceive()
            .SaveDraftAsync(id, Arg.Any<SaveDraftRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveDraft_WhenWrongNit_ThrowsTemplateNotFound()
    {
        var id = Guid.NewGuid();
        var bundle = new TemplateBundleDto(
            new TemplateDto(id, "Demo", "factura", "draft", 1, DateTimeOffset.UtcNow, "900000000", false),
            Array.Empty<TemplateVersionDto>());
        _catalog.GetBundleAsync(id, Arg.Any<CancellationToken>()).Returns(bundle);

        var sut = new SaveDraftUseCase(_catalog, new SaveDraftRequestValidator());
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.ExecuteAsync(id, new SaveDraftRequest(Status: "published"), "111111111"));

        Assert.Equal(AppErrorCodes.TemplateNotFound, ex.Code);
    }

    [Fact]
    public async Task SaveDraft_WhenDraftWithoutHtml_ThrowsValidationError()
    {
        var sut = new SaveDraftUseCase(_catalog, new SaveDraftRequestValidator());

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.ExecuteAsync(Guid.NewGuid(), new SaveDraftRequest(Status: "draft"), "900000000"));

        Assert.Equal(AppErrorCodes.ValidationError, ex.Code);
    }

    [Fact]
    public async Task Get_WhenMissing_ThrowsTemplateNotFound()
    {
        var id = Guid.NewGuid();
        _catalog.GetBundleAsync(id, Arg.Any<CancellationToken>()).Returns((TemplateBundleDto?)null);

        var sut = new GetTemplateUseCase(_catalog);
        var ex = await Assert.ThrowsAsync<AppException>(() => sut.ExecuteAsync(id, "900000000"));

        Assert.Equal(AppErrorCodes.TemplateNotFound, ex.Code);
    }

    [Fact]
    public async Task Get_WhenWrongNit_ThrowsTemplateNotFound()
    {
        var id = Guid.NewGuid();
        var bundle = new TemplateBundleDto(
            new TemplateDto(id, "Demo", "factura", "draft", 1, DateTimeOffset.UtcNow, "900000000", false),
            Array.Empty<TemplateVersionDto>());
        _catalog.GetBundleAsync(id, Arg.Any<CancellationToken>()).Returns(bundle);

        var sut = new GetTemplateUseCase(_catalog);
        var ex = await Assert.ThrowsAsync<AppException>(() => sut.ExecuteAsync(id, "111111111"));

        Assert.Equal(AppErrorCodes.TemplateNotFound, ex.Code);
    }

    [Fact]
    public async Task Archive_WhenWrongNit_ThrowsTemplateNotFound()
    {
        var id = Guid.NewGuid();
        var bundle = new TemplateBundleDto(
            new TemplateDto(id, "Demo", "factura", "published", 1, DateTimeOffset.UtcNow, "900000000", false),
            Array.Empty<TemplateVersionDto>());
        _catalog.GetBundleAsync(id, Arg.Any<CancellationToken>()).Returns(bundle);

        var sut = new ArchiveTemplateUseCase(_catalog);
        var ex = await Assert.ThrowsAsync<AppException>(() => sut.ExecuteAsync(id, "111111111"));

        Assert.Equal(AppErrorCodes.TemplateNotFound, ex.Code);
        await _catalog.DidNotReceive().ArchiveAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Archive_WhenOwned_CallsCatalog()
    {
        var id = Guid.NewGuid();
        var bundle = new TemplateBundleDto(
            new TemplateDto(id, "Demo", "factura", "published", 1, DateTimeOffset.UtcNow, "900000000", false),
            Array.Empty<TemplateVersionDto>());
        _catalog.GetBundleAsync(id, Arg.Any<CancellationToken>()).Returns(bundle);

        var sut = new ArchiveTemplateUseCase(_catalog);
        await sut.ExecuteAsync(id, "900000000");

        await _catalog.Received(1).ArchiveAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenPublished_MapsToConflict()
    {
        var id = Guid.NewGuid();
        var bundle = new TemplateBundleDto(
            new TemplateDto(id, "Demo", "factura", "published", 1, DateTimeOffset.UtcNow, "900000000", false),
            Array.Empty<TemplateVersionDto>());
        _catalog.GetBundleAsync(id, Arg.Any<CancellationToken>()).Returns(bundle);
        _catalog.When(c => c.DeleteAsync(id, Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("La plantilla ya fue publicada."));

        var sut = new DeleteTemplateUseCase(_catalog);
        var ex = await Assert.ThrowsAsync<AppException>(() => sut.ExecuteAsync(id, "900000000"));

        Assert.Equal(AppErrorCodes.Conflict, ex.Code);
    }

    [Fact]
    public async Task Delete_WhenUnusedDraft_CallsCatalog()
    {
        var id = Guid.NewGuid();
        var bundle = new TemplateBundleDto(
            new TemplateDto(id, "Demo", "factura", "draft", 1, DateTimeOffset.UtcNow, "900000000", false),
            Array.Empty<TemplateVersionDto>());
        _catalog.GetBundleAsync(id, Arg.Any<CancellationToken>()).Returns(bundle);

        var sut = new DeleteTemplateUseCase(_catalog);
        await sut.ExecuteAsync(id, "900000000");

        await _catalog.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }
}
