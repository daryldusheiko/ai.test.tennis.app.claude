using Microsoft.Playwright;

namespace SocialTennis.BrowserTests;

/// <summary>Gives each test a brand-new browser context (empty storage, like a fresh install) sized as an iPhone SE.</summary>
public abstract class AppTestBase
{
    protected IBrowserContext Context { get; private set; } = null!;

    protected IPage Page { get; private set; } = null!;

    [SetUp]
    public async Task OpenContextAsync()
    {
        Context = await BrowserSetUp.Browser.NewContextAsync(BrowserSetUp.Playwright.Devices["iPhone SE"]);
        Page = await Context.NewPageAsync();
        Page.SetDefaultTimeout(30_000);
    }

    [TearDown]
    public async Task CloseContextAsync() => await Context.CloseAsync();

    protected static string RootUrl(string path = "") => new Uri(PublishedSite.Root.BaseAddress, path).ToString();

    protected static string SubUrl(string path = "") => new Uri(PublishedSite.Sub.BaseAddress, path).ToString();

    protected async Task WaitForAppAsync(IPage? page = null)
    {
        page ??= Page;
        await Assertions.Expect(page.Locator("h1#page-title")).Not.ToBeEmptyAsync();
    }

    /// <summary>Waits until the service worker has cached everything and controls the page, so offline use is possible.</summary>
    protected async Task WaitForOfflineReadyAsync(IPage? page = null)
    {
        page ??= Page;
        await page.WaitForFunctionAsync("navigator.serviceWorker.controller !== null");
    }
}
