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
            sut.ExecuteAsync(new CreateTemplateRequest(Name: "", DocumentType: "factura")));

        Assert.Equal(AppErrorCodes.ValidationError, ex.Code);
    }

    [Fact]
    public async Task Create_WhenValid_ReturnsCreatedTemplate()
    {
        var request = new CreateTemplateRequest(Name: "Demo", DocumentType: "factura");
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
    public async Task Publish_WhenMissing_ThrowsTemplateNotFound()
    {
        var id = Guid.NewGuid();
        _catalog.PublishAsync(id, Arg.Any<CancellationToken>())
            .Returns<Task<TemplateVersionDto>>(_ => throw new KeyNotFoundException());

        var sut = new PublishTemplateUseCase(_catalog);
        var ex = await Assert.ThrowsAsync<AppException>(() => sut.ExecuteAsync(id));

        Assert.Equal(AppErrorCodes.TemplateNotFound, ex.Code);
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
