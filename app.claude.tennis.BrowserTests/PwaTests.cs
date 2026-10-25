using System.Text.Json;
using Microsoft.Playwright;

namespace SocialTennis.BrowserTests;

public class PwaTests : AppTestBase
{
    private static readonly string[] IconSizes = ["192x192", "512x512"];

    private static readonly string[] IconFiles = ["icon-192.png", "icon-512.png", "favicon.png"];

    [Test]
    public async Task Page_OnOpen_IsTitledSocialTennis()
    {
        await Page.GotoAsync(RootUrl());

        await Assertions.Expect(Page).ToHaveTitleAsync("Social Tennis");
    }

    [Test]
    public async Task Page_OnOpen_HasIPhoneInstallMetadata()
    {
        await Page.GotoAsync(RootUrl());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await MetaContentAsync("apple-mobile-web-app-capable"), Is.EqualTo("yes"));
            Assert.That(await MetaContentAsync("apple-mobile-web-app-title"), Is.EqualTo("Tennis"));
            Assert.That(await MetaContentAsync("viewport"), Does.Contain("viewport-fit=cover"));
        }
    }

    [Test]
    public async Task AppleTouchIcon_Requested_IsA180PixelPng()
    {
        await Page.GotoAsync(RootUrl());
        var href = await Page.Locator("link[rel=apple-touch-icon]").GetAttributeAsync("href");

        var response = await Context.APIRequest.GetAsync(new Uri(new Uri(Page.Url), href).ToString());
        var size = await Page.EvaluateAsync<int[]>(
            """
            async url => {
                const bitmap = await createImageBitmap(await (await fetch(url)).blob());
                return [bitmap.width, bitmap.height];
            }
            """,
            new Uri(new Uri(Page.Url), href).ToString());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(response.Headers["content-type"], Is.EqualTo("image/png"));
            Assert.That(size, Is.EqualTo([180, 180]));
        }
    }

    [Test]
    public async Task Manifest_Requested_DescribesAFullScreenPortraitApp()
    {
        await Page.GotoAsync(RootUrl());
        var href = await Page.Locator("link[rel=manifest]").GetAttributeAsync("href");

        var response = await Context.APIRequest.GetAsync(new Uri(new Uri(Page.Url), href).ToString());
        using var manifest = JsonDocument.Parse(await response.TextAsync());
        var root = manifest.RootElement;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.GetProperty("name").GetString(), Is.EqualTo("Social Tennis"));
            Assert.That(root.GetProperty("short_name").GetString(), Is.EqualTo("Tennis"));
            Assert.That(root.GetProperty("display").GetString(), Is.EqualTo("standalone"));
            Assert.That(root.GetProperty("orientation").GetString(), Is.EqualTo("portrait"));
            Assert.That(root.GetProperty("start_url").GetString(), Is.EqualTo("./"));
            Assert.That(root.GetProperty("icons").EnumerateArray().Select(icon => icon.GetProperty("sizes").GetString()), Is.EquivalentTo(IconSizes));
        }
    }

    [Test]
    public async Task Manifest_Icons_AreServed()
    {
        await Page.GotoAsync(RootUrl());

        foreach (var icon in IconFiles)
        {
            var response = await Context.APIRequest.GetAsync(RootUrl(icon));
            Assert.That(response.Status, Is.EqualTo(200), icon);
        }
    }

    [Test]
    public async Task App_AfterFirstLoad_WorksWithTheServerUnreachable()
    {
        await Page.GotoAsync(RootUrl());
        await WaitForAppAsync();
        await WaitForOfflineReadyAsync();

        await PublishedSite.Root.StopAsync();
        try
        {
            await Page.ReloadAsync();
            await WaitForAppAsync();

            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("Play");
            await Page.GetByRole(AriaRole.Navigation, new() { Name = "Main" }).GetByRole(AriaRole.Link, new() { Name = "About", Exact = true }).ClickAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("About");
        }
        finally
        {
            await PublishedSite.Root.StartAsync();
        }
    }

    [Test]
    public async Task App_ClosedAndReopenedOffline_StillOpensAtADeepLink()
    {
        await Page.GotoAsync(RootUrl());
        await WaitForAppAsync();
        await WaitForOfflineReadyAsync();

        await PublishedSite.Root.StopAsync();
        try
        {
            await Page.CloseAsync();
            var reopened = await Context.NewPageAsync();
            await reopened.GotoAsync(RootUrl("players"));

            await Assertions.Expect(reopened.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("Players");
        }
        finally
        {
            await PublishedSite.Root.StartAsync();
        }
    }

    [Test]
    public async Task App_ServedFromRepositorySubpath_WorksOffline()
    {
        await Page.GotoAsync(SubUrl());
        await WaitForAppAsync();
        await WaitForOfflineReadyAsync();

        await PublishedSite.Sub.StopAsync();
        try
        {
            await Page.GotoAsync(SubUrl("schedule"));
            await WaitForAppAsync();

            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("Schedule");
        }
        finally
        {
            await PublishedSite.Sub.StartAsync();
        }
    }

    private async Task<string?> MetaContentAsync(string name) =>
        await Page.Locator($"meta[name={name}]").GetAttributeAsync("content");
}
