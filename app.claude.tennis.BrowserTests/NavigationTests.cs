using Microsoft.Playwright;

namespace SocialTennis.BrowserTests;

public class NavigationTests : AppTestBase
{
    private static readonly string[] Labels = ["Play", "Players", "Schedule", "About"];

    private ILocator Toolbar => Page.GetByRole(AriaRole.Navigation, new() { Name = "Main" });

    private ILocator Header => Page.GetByRole(AriaRole.Heading, new() { Level = 1 });

    [Test]
    public async Task Toolbar_OnOpen_ShowsFourItemsInOrder()
    {
        await Page.GotoAsync(RootUrl());
        await WaitForAppAsync();

        var items = Toolbar.GetByRole(AriaRole.Link);
        await Assertions.Expect(items).ToHaveCountAsync(4);
        await Assertions.Expect(items).ToHaveTextAsync(Labels);
    }

    [Test]
    public async Task Toolbar_OnIPhoneSe_IsAtTheBottomBelowTheContent()
    {
        await Page.GotoAsync(RootUrl());
        await WaitForAppAsync();

        var toolbar = await Toolbar.BoundingBoxAsync() ?? throw new InvalidOperationException("The toolbar is not visible.");
        var header = await Header.BoundingBoxAsync() ?? throw new InvalidOperationException("The header is not visible.");
        var viewport = Page.ViewportSize ?? throw new InvalidOperationException("The page has no viewport.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(toolbar.Y + toolbar.Height, Is.EqualTo(viewport.Height).Within(1));
            Assert.That(header.Y, Is.LessThan(toolbar.Y));
            Assert.That(header.Y, Is.Zero.Within(1));
        }
    }

    [Test]
    public async Task Toolbar_Items_EachHaveAnIcon()
    {
        await Page.GotoAsync(RootUrl());
        await WaitForAppAsync();

        await Assertions.Expect(Toolbar.Locator("svg")).ToHaveCountAsync(4);
    }

    [Test]
    public async Task Header_OnOpenWithNothingInPlay_ShowsPlayers()
    {
        await Page.GotoAsync(RootUrl());

        await Assertions.Expect(Header).ToHaveTextAsync("Players");
    }

    [TestCase("Players")]
    [TestCase("Schedule")]
    [TestCase("About")]
    [TestCase("Play")]
    public async Task Header_AfterTappingToolbarItem_ShowsThatPageLabel(string label)
    {
        await Page.GotoAsync(RootUrl("about"));
        await WaitForAppAsync();

        await Toolbar.GetByRole(AriaRole.Link, new() { Name = label, Exact = true }).ClickAsync();

        await Assertions.Expect(Header).ToHaveTextAsync(label);
    }

    [Test]
    public async Task Header_WalkingThroughEveryItem_FollowsEachTap()
    {
        await Page.GotoAsync(RootUrl());
        await WaitForAppAsync();

        foreach (var label in Labels)
        {
            await Toolbar.GetByRole(AriaRole.Link, new() { Name = label, Exact = true }).ClickAsync();
            await Assertions.Expect(Header).ToHaveTextAsync(label);
        }
    }

    [Test]
    public async Task Toolbar_CurrentPage_IsHighlighted()
    {
        await Page.GotoAsync(RootUrl("schedule"));
        await WaitForAppAsync();

        await Assertions.Expect(Toolbar.GetByRole(AriaRole.Link, new() { Name = "Schedule", Exact = true })).ToHaveClassAsync("toolbar-item active");
        await Assertions.Expect(Toolbar.GetByRole(AriaRole.Link, new() { Name = "Play", Exact = true })).Not.ToHaveClassAsync("toolbar-item active");
    }

    [Test]
    public async Task UnknownAddress_Opened_ShowsNotFoundMessage()
    {
        await Page.GotoAsync(RootUrl("no/such/page"));

        await Assertions.Expect(Page.GetByText("Sorry, the content you are looking for does not exist.")).ToBeVisibleAsync();
        await Assertions.Expect(Toolbar.GetByRole(AriaRole.Link)).ToHaveCountAsync(4);
    }

    [TestCase("")]
    [TestCase("players")]
    [TestCase("schedule")]
    [TestCase("about")]
    [TestCase("no/such/page")]
    public async Task Page_OnIPhoneSe_DoesNotScrollHorizontally(string path)
    {
        await Page.GotoAsync(RootUrl(path));
        await WaitForAppAsync();

        var overflow = await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth");

        Assert.That(overflow, Is.False);
    }

    [TestCase("players", "Players")]
    [TestCase("schedule", "Schedule")]
    [TestCase("about", "About")]
    public async Task DeepLink_Opened_ShowsThatPage(string path, string label)
    {
        await Page.GotoAsync(RootUrl(path));

        await Assertions.Expect(Header).ToHaveTextAsync(label);
    }

    [TestCase("players", "Players")]
    [TestCase("about", "About")]
    public async Task DeepLink_UnderRepositorySubpath_ShowsThatPage(string path, string label)
    {
        await Page.GotoAsync(SubUrl(path));

        await Assertions.Expect(Header).ToHaveTextAsync(label);
    }

    [Test]
    public async Task Toolbar_UnderRepositorySubpath_NavigatesWithinTheSubpath()
    {
        await Page.GotoAsync(SubUrl());
        await WaitForAppAsync();

        await Toolbar.GetByRole(AriaRole.Link, new() { Name = "Schedule", Exact = true }).ClickAsync();

        await Assertions.Expect(Header).ToHaveTextAsync("Schedule");
        Assert.That(Page.Url, Is.EqualTo(SubUrl("schedule")));
    }
}
