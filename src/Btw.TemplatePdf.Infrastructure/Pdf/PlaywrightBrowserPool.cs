using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Btw.TemplatePdf.Infrastructure.Pdf;

/// <summary>
/// Shared Chromium instance: warm on startup, reuse across PDF renders.
/// </summary>
public sealed class PlaywrightBrowserPool : IAsyncDisposable
{
    private readonly ILogger<PlaywrightBrowserPool> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _disposed;

    public PlaywrightBrowserPool(ILogger<PlaywrightBrowserPool> logger)
    {
        _logger = logger;
    }

    public async Task WarmAsync(CancellationToken cancellationToken = default)
    {
        await EnsureBrowserAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IPage> NewPageAsync(CancellationToken cancellationToken = default)
    {
        var browser = await EnsureBrowserAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await browser.NewPageAsync().ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            await ResetAsync().ConfigureAwait(false);
            browser = await EnsureBrowserAsync(cancellationToken).ConfigureAwait(false);
            return await browser.NewPageAsync().ConfigureAwait(false);
        }
    }

    private async Task<IBrowser> EnsureBrowserAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_browser is { IsConnected: true })
            return _browser;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_browser is { IsConnected: true })
                return _browser;

            await DisposeBrowserUnlockedAsync().ConfigureAwait(false);

            _logger.LogInformation("Starting shared Playwright Chromium for PDF rendering…");
            _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            _browser = await _playwright.Chromium
                .LaunchAsync(new BrowserTypeLaunchOptions { Headless = true })
                .ConfigureAwait(false);
            _logger.LogInformation("Playwright Chromium is warm.");
            return _browser;
        }
        catch (PlaywrightException ex) when (ex.Message.Contains(
                                                 "Executable doesn't exist",
                                                 StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Playwright Chromium is not installed. Run: pwsh bin/Debug/net10.0/playwright.ps1 install chromium",
                ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ResetAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisposeBrowserUnlockedAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task DisposeBrowserUnlockedAsync()
    {
        if (_browser is not null)
        {
            try
            {
                await _browser.CloseAsync().ConfigureAwait(false);
            }
            catch
            {
                /* ignore */
            }

            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisposeBrowserUnlockedAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}

public sealed class PlaywrightWarmupHostedService : IHostedService
{
    private readonly PlaywrightBrowserPool _pool;
    private readonly ILogger<PlaywrightWarmupHostedService> _logger;

    public PlaywrightWarmupHostedService(
        PlaywrightBrowserPool pool,
        ILogger<PlaywrightWarmupHostedService> logger)
    {
        _pool = pool;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _pool.WarmAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Do not block app start if Chromium is missing in a given environment.
            _logger.LogWarning(
                ex,
                "Playwright warm-up skipped; first PDF render will retry launching Chromium.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
