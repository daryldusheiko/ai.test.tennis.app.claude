using Microsoft.Playwright;

namespace SocialTennis.BrowserTests;

public partial class AboutTests : AppTestBase
{
    private ILocator Header => Page.GetByRole(AriaRole.Heading, new() { Level = 1 });

    private ILocator Root => Page.Locator("html");

    private async Task OpenAboutAsync()
    {
        await Page.GotoAsync(RootUrl("about"));
        await Assertions.Expect(Header).ToHaveTextAsync("About");
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "How to use" })).ToBeVisibleAsync();
    }

    private ILocator Theme(string name) => Page.GetByRole(AriaRole.Radio, new() { Name = name, Exact = true });

    [Test]
    public async Task About_StampedPublish_ShowsTheVersionCommitAndReleaseDate()
    {
        await OpenAboutAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Social Tennis" })).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText($"Version {PublishedSite.StampedVersion} ({PublishedSite.StampedCommit})")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("Released 7 March 2026")).ToBeVisibleAsync();
    }

    [Test]
    public async Task About_StampedPublish_ServiceWorkerCarriesTheVersionSoInstalledCopiesUpdate()
    {
        var script = await Page.APIRequest.GetAsync(RootUrl("service-worker.js"));

        Assert.That(await script.TextAsync(), Does.Contain($"Released {PublishedSite.StampedVersion} ({PublishedSite.StampedCommit}, {PublishedSite.StampedReleased})"));
    }

    [Test]
    public async Task About_OnOpen_ShowsTheHowToUseInstructions()
    {
        await OpenAboutAsync();

        var steps = Page.Locator(".how-to li");
        await Assertions.Expect(steps).ToHaveCountAsync(5);
        foreach (var topic in new[] { "Players:", "Schedule:", "Review:", "Play:", "Finish:" })
        {
            await Assertions.Expect(Page.Locator(".how-to strong").Filter(new() { HasText = topic })).ToBeVisibleAsync();
        }

        await Assertions.Expect(Page.GetByText("Close schedule on the Play page when the session is over.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("The app works offline once installed")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("Save a backup from the Players page now and then.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Theme_OnFirstOpen_OffersLightDarkAndDeviceWithDeviceChosen()
    {
        await OpenAboutAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Radio)).ToHaveCountAsync(3);
        await Assertions.Expect(Theme("Device")).ToBeCheckedAsync();
        await Assertions.Expect(Root).Not.ToHaveAttributeAsync("data-theme", AnyValue());
    }

    [Test]
    public async Task Theme_Dark_IsAppliedAndRemembered()
    {
        await OpenAboutAsync();

        await Theme("Dark").CheckAsync();

        await Assertions.Expect(Root).ToHaveAttributeAsync("data-theme", "dark");
        var background = await Page.EvaluateAsync<string>("getComputedStyle(document.body).backgroundColor");
        Assert.That(background, Is.EqualTo("rgb(16, 19, 17)"));

        await Page.ReloadAsync();
        await Assertions.Expect(Theme("Dark")).ToBeCheckedAsync();
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-theme", "dark");
    }

    [Test]
    public async Task Theme_Light_OverridesADarkPhone()
    {
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await OpenAboutAsync();

        await Theme("Light").CheckAsync();

        await Assertions.Expect(Root).ToHaveAttributeAsync("data-theme", "light");
        var background = await Page.EvaluateAsync<string>("getComputedStyle(document.body).backgroundColor");
        Assert.That(background, Is.EqualTo("rgb(255, 255, 255)"));
    }

    [Test]
    public async Task Theme_ChosenThenDevice_FollowsThePhoneAgain()
    {
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await OpenAboutAsync();
        await Theme("Light").CheckAsync();

        await Theme("Device").CheckAsync();

        await Assertions.Expect(Root).Not.ToHaveAttributeAsync("data-theme", AnyValue());
        var background = await Page.EvaluateAsync<string>("getComputedStyle(document.body).backgroundColor");
        Assert.That(background, Is.EqualTo("rgb(16, 19, 17)"));
    }

    [Test]
    public async Task Theme_StoredChoice_IsAppliedBeforeTheAppStartsSoThereIsNoFlash()
    {
        await Page.AddInitScriptAsync("localStorage.setItem('socialTennis.theme', 'dark');");

        await Page.GotoAsync(RootUrl("about"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        // index.html's own script runs while the page is still loading, long before the app draws anything.
        var attribute = await Page.EvaluateAsync<string?>("document.documentElement.getAttribute('data-theme')");
        Assert.That(attribute, Is.EqualTo("dark"));
    }

    [Test]
    public async Task Theme_RadioButtons_HaveAccessibleNamesAndFitTheScreen()
    {
        await OpenAboutAsync();

        foreach (var radio in await Page.GetByRole(AriaRole.Radio).AllAsync())
        {
            var box = await radio.BoundingBoxAsync() ?? throw new InvalidOperationException("A theme option is hidden.");
            Assert.That(box.X + box.Width, Is.LessThanOrEqualTo(Page.ViewportSize!.Width));
        }

        var overflow = await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth");
        Assert.That(overflow, Is.False);
    }

    [Test]
    public async Task About_Offline_StillShowsTheVersionAndTheme()
    {
        await Page.GotoAsync(RootUrl("about"));
        await WaitForAppAsync();
        await WaitForOfflineReadyAsync();

        await PublishedSite.Root.StopAsync();
        try
        {
            await Page.ReloadAsync();

            await Assertions.Expect(Page.GetByText($"Version {PublishedSite.StampedVersion} ({PublishedSite.StampedCommit})")).ToBeVisibleAsync();
            await Assertions.Expect(Theme("Device")).ToBeCheckedAsync();
        }
        finally
        {
            await PublishedSite.Root.StartAsync();
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(".+")]
    private static partial System.Text.RegularExpressions.Regex AnyValue();
}
