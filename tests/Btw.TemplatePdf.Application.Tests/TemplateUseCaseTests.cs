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
            Nit: "900000000");
        var expected = new TemplateDto(
            Guid.NewGuid(),
            "Demo",
            "factura",
            "draft",
            1,
            DateTimeOffset.UtcNow,
            "900000000",
            false);
        _catalog.CreateAsync(request, Arg.Any<CancellationToken>()).Returns(expected);

        var sut = new CreateTemplateUseCase(_catalog, new CreateTemplateRequestValidator());
        var result = await sut.ExecuteAsync(request);

        Assert.Equal("Demo", result.Name);
        Assert.Equal("draft", result.Status);
    }

    [Fact]
    public async Task SaveDraft_WhenStatusPublishedAndMissing_ThrowsTemplateNotFound()
    {
        var id = Guid.NewGuid();
        _catalog.PublishAsync(id, Arg.Any<CancellationToken>())
            .Returns<Task<TemplateVersionDto>>(_ => throw new KeyNotFoundException());

        var sut = new SaveDraftUseCase(_catalog, new SaveDraftRequestValidator());
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.ExecuteAsync(id, new SaveDraftRequest(Status: "published")));

        Assert.Equal(AppErrorCodes.TemplateNotFound, ex.Code);
    }

    [Fact]
    public async Task SaveDraft_WhenStatusPublished_CallsPublish()
    {
        var id = Guid.NewGuid();
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
        _catalog.PublishAsync(id, Arg.Any<CancellationToken>()).Returns(version);

        var sut = new SaveDraftUseCase(_catalog, new SaveDraftRequestValidator());
        var result = await sut.ExecuteAsync(id, new SaveDraftRequest(Status: "published"));

        Assert.True(result.IsPublished);
        await _catalog.Received(1).PublishAsync(id, Arg.Any<CancellationToken>());
        await _catalog.DidNotReceive()
            .SaveDraftAsync(id, Arg.Any<SaveDraftRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveDraft_WhenDraftWithoutHtml_ThrowsValidationError()
    {
        var sut = new SaveDraftUseCase(_catalog, new SaveDraftRequestValidator());

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.ExecuteAsync(Guid.NewGuid(), new SaveDraftRequest(Status: "draft")));

        Assert.Equal(AppErrorCodes.ValidationError, ex.Code);
    }

    [Fact]
    public async Task Get_WhenMissing_ThrowsTemplateNotFound()
    {
        var id = Guid.NewGuid();
        _catalog.GetBundleAsync(id, Arg.Any<CancellationToken>()).Returns((TemplateBundleDto?)null);

        var sut = new GetTemplateUseCase(_catalog);
        var ex = await Assert.ThrowsAsync<AppException>(() => sut.ExecuteAsync(id));

        Assert.Equal(AppErrorCodes.TemplateNotFound, ex.Code);
    }
}
