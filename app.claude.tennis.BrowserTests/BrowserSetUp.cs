using Microsoft.Playwright;

namespace SocialTennis.BrowserTests;

/// <summary>Publishes the app and starts WebKit once for the whole run. The only manual step is none: WebKit is downloaded on first use.</summary>
[SetUpFixture]
public class BrowserSetUp
{
    internal static IPlaywright Playwright { get; private set; } = null!;

    internal static IBrowser Browser { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        var exitCode = Program.Main(["install", "webkit"]);
        Assert.That(exitCode, Is.Zero, "Playwright could not install WebKit");

        await PublishedSite.PublishAsync();
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Webkit.LaunchAsync();
    }

    [OneTimeTearDown]
    public async Task TearDownAsync()
    {
        await Browser.CloseAsync();
        Playwright.Dispose();
        await PublishedSite.CleanUpAsync();
    }
}
