using Microsoft.Playwright;

namespace SocialTennis.BrowserTests;

public class PlayersTests : AppTestBase
{
    private ILocator Header => Page.GetByRole(AriaRole.Heading, new() { Level = 1 });

    private ILocator Names => Page.Locator(".player-name");

    private ILocator Sort => Page.GetByLabel("Sort by");

    private async Task OpenPlayersAsync(IPage? page = null, string path = "players")
    {
        page ??= Page;
        await page.GotoAsync(RootUrl(path));
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("Players");
    }

    private async Task AddPlayerAsync(string first, string last = "", string? rank = null, string comment = "", IPage? page = null)
    {
        page ??= Page;
        await page.GetByRole(AriaRole.Link, new() { Name = "Add player", Exact = true }).ClickAsync();
        await page.GetByLabel("First name").FillAsync(first);
        await page.GetByLabel("Last name").FillAsync(last);
        if (rank is not null)
        {
            await page.GetByLabel("Rank").SelectOptionAsync(rank);
        }

        await page.GetByLabel("Comment").FillAsync(comment);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Add player", Exact = true })).ToBeVisibleAsync();
    }

    private async Task AddSamplePlayersAsync()
    {
        await AddPlayerAsync("Bob", "Adams");
        await AddPlayerAsync("Ann", "Smith");
        await AddPlayerAsync("Cat", "Zed");
    }

    [Test]
    public async Task Players_NoneYet_ShowsEmptyMessage()
    {
        await OpenPlayersAsync();

        await Assertions.Expect(Page.GetByText("No players yet. Add your first player.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Players_OnOpen_ShowsSearchSortAndAddControls()
    {
        await OpenPlayersAsync();

        await Assertions.Expect(Page.GetByPlaceholder("Search players")).ToBeVisibleAsync();
        await Assertions.Expect(Sort).ToHaveValueAsync("FirstNameAscending");
        await Assertions.Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Add player", Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task AddPlayer_FilledIn_AppearsInTheListWithAnEditButton()
    {
        await OpenPlayersAsync();

        await AddPlayerAsync("Ann", "Smith", "2", "Left handed");

        await Assertions.Expect(Names).ToHaveTextAsync(["Ann Smith"]);
        await Assertions.Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Edit Ann Smith" })).ToHaveTextAsync("Edit");
    }

    [Test]
    public async Task AddPlayer_WithoutFirstName_ShowsErrorAndStaysOnForm()
    {
        await OpenPlayersAsync();
        await Page.GetByRole(AriaRole.Link, new() { Name = "Add player", Exact = true }).ClickAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Assertions.Expect(Page.GetByText("First name is required.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Add player" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task AddPlayer_FormRankDropDown_OffersNoneAndOneToThree()
    {
        await OpenPlayersAsync();
        await Page.GetByRole(AriaRole.Link, new() { Name = "Add player", Exact = true }).ClickAsync();

        await Assertions.Expect(Page.GetByLabel("Rank").Locator("option")).ToHaveTextAsync(["None", "1", "2", "3"]);
    }

    [Test]
    public async Task AddPlayer_TooLongNameAndComment_ShowsErrors()
    {
        await OpenPlayersAsync();
        await Page.GetByRole(AriaRole.Link, new() { Name = "Add player", Exact = true }).ClickAsync();
        await Page.GetByLabel("First name").FillAsync(new string('a', 51));
        await Page.GetByLabel("Last name").FillAsync(new string('b', 51));
        await Page.GetByLabel("Comment").FillAsync(new string('c', 501));

        await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Assertions.Expect(Page.GetByText("First name must be 50 characters or fewer.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("Last name must be 50 characters or fewer.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("Comment must be 500 characters or fewer.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task EditPlayer_ChangingFields_SavesThemAndShowsThemAgain()
    {
        await OpenPlayersAsync();
        await AddPlayerAsync("Ann", "Smith", "1", "Hello");

        await Page.GetByRole(AriaRole.Link, new() { Name = "Edit Ann Smith" }).ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Edit player" })).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByLabel("Rank")).ToHaveValueAsync("1");
        await Assertions.Expect(Page.GetByLabel("Comment")).ToHaveValueAsync("Hello");
        await Page.GetByLabel("First name").FillAsync("Anna");
        await Page.GetByLabel("Rank").SelectOptionAsync(string.Empty);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Assertions.Expect(Names).ToHaveTextAsync(["Anna Smith"]);
        await Page.GetByRole(AriaRole.Link, new() { Name = "Edit Anna Smith" }).ClickAsync();
        await Assertions.Expect(Page.GetByLabel("Rank")).ToHaveValueAsync(string.Empty);
    }

    [Test]
    public async Task EditPlayer_BackLink_ReturnsToTheList()
    {
        await OpenPlayersAsync();
        await AddPlayerAsync("Ann");
        await Page.GetByRole(AriaRole.Link, new() { Name = "Edit Ann" }).ClickAsync();

        await Page.GetByRole(AriaRole.Link, new() { Name = "‹ Players" }).ClickAsync();

        await Assertions.Expect(Names).ToHaveTextAsync(["Ann"]);
    }

    [Test]
    public async Task EditPlayer_UnknownId_SaysPlayerNoLongerExists()
    {
        await OpenPlayersAsync(path: "players/edit/00000000-0000-0000-0000-000000000001");

        await Assertions.Expect(Page.GetByText("This player no longer exists.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task DeletePlayer_Confirmed_RemovesThemFromTheList()
    {
        await OpenPlayersAsync();
        await AddSamplePlayersAsync();
        await Page.GetByRole(AriaRole.Link, new() { Name = "Edit Ann Smith" }).ClickAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete player" }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Alertdialog);
        await Assertions.Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = "Delete player?" })).ToBeVisibleAsync();
        await Assertions.Expect(dialog.GetByText("Ann Smith will be removed. This can't be undone.")).ToBeVisibleAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();

        await Assertions.Expect(Names).ToHaveTextAsync(["Bob Adams", "Cat Zed"]);
    }

    [Test]
    public async Task DeletePlayer_Cancelled_KeepsThePlayer()
    {
        await OpenPlayersAsync();
        await AddPlayerAsync("Ann", "Smith");
        await Page.GetByRole(AriaRole.Link, new() { Name = "Edit Ann Smith" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete player" }).ClickAsync();

        await Page.GetByRole(AriaRole.Alertdialog).GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Alertdialog)).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Link, new() { Name = "‹ Players" }).ClickAsync();
        await Assertions.Expect(Names).ToHaveTextAsync(["Ann Smith"]);
    }

    [Test]
    public async Task DeleteDialog_OnIPhoneSe_FitsTheScreenAndIsCentred()
    {
        await OpenPlayersAsync();
        await AddPlayerAsync(new string('W', 50), new string('W', 50));
        await Page.GetByRole(AriaRole.Link, new() { Name = "Edit" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete player" }).ClickAsync();

        var box = await Page.GetByRole(AriaRole.Alertdialog).BoundingBoxAsync() ?? throw new InvalidOperationException("The dialog is not visible.");
        var viewport = Page.ViewportSize ?? throw new InvalidOperationException("The page has no viewport.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(box.X, Is.GreaterThanOrEqualTo(0));
            Assert.That(box.Y, Is.GreaterThanOrEqualTo(0));
            Assert.That(box.X + box.Width, Is.LessThanOrEqualTo(viewport.Width));
            Assert.That(box.Y + box.Height, Is.LessThanOrEqualTo(viewport.Height));
            Assert.That(box.Y + (box.Height / 2), Is.EqualTo(viewport.Height / 2.0).Within(2));
        }

        await Assertions.Expect(Page.GetByRole(AriaRole.Alertdialog).GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true })).ToBeInViewportAsync();
    }

    [Test]
    public async Task Search_Typed_FiltersAsYouTypeIgnoringCaseWithEveryWordMatching()
    {
        await OpenPlayersAsync();
        await AddSamplePlayersAsync();
        var search = Page.GetByPlaceholder("Search players");

        await search.PressSequentiallyAsync("AN");
        await Assertions.Expect(Names).ToHaveTextAsync(["Ann Smith"]);

        await search.FillAsync("ann sm");
        await Assertions.Expect(Names).ToHaveTextAsync(["Ann Smith"]);

        await search.FillAsync("zed");
        await Assertions.Expect(Names).ToHaveTextAsync(["Cat Zed"]);
    }

    [Test]
    public async Task Search_NoMatch_ShowsMessageWithTheSearch()
    {
        await OpenPlayersAsync();
        await AddSamplePlayersAsync();

        await Page.GetByPlaceholder("Search players").FillAsync("xyz");

        await Assertions.Expect(Page.GetByText("No players match “xyz”.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Sort_EachOption_OrdersTheList()
    {
        await OpenPlayersAsync();
        await AddPlayerAsync("Bob", "Adams");
        await AddPlayerAsync("Ann", "Smith");
        await AddPlayerAsync("Cat", "Zed");
        await Assertions.Expect(Sort.Locator("option")).ToHaveTextAsync(["First name (A–Z)", "First name (Z–A)", "Last name (A–Z)", "Last name (Z–A)"]);

        await Assertions.Expect(Names).ToHaveTextAsync(["Ann Smith", "Bob Adams", "Cat Zed"]);
        await Sort.SelectOptionAsync(new SelectOptionValue { Label = "First name (Z–A)" });
        await Assertions.Expect(Names).ToHaveTextAsync(["Cat Zed", "Bob Adams", "Ann Smith"]);
        await Sort.SelectOptionAsync(new SelectOptionValue { Label = "Last name (A–Z)" });
        await Assertions.Expect(Names).ToHaveTextAsync(["Bob Adams", "Ann Smith", "Cat Zed"]);
        await Sort.SelectOptionAsync(new SelectOptionValue { Label = "Last name (Z–A)" });
        await Assertions.Expect(Names).ToHaveTextAsync(["Cat Zed", "Ann Smith", "Bob Adams"]);
    }

    [Test]
    public async Task Sort_Chosen_IsRememberedWhenTheAppIsClosedAndReopened()
    {
        await OpenPlayersAsync();
        await AddSamplePlayersAsync();
        await Sort.SelectOptionAsync(new SelectOptionValue { Label = "Last name (Z–A)" });
        await Assertions.Expect(Names).ToHaveTextAsync(["Cat Zed", "Ann Smith", "Bob Adams"]);

        await Page.CloseAsync();
        var reopened = await Context.NewPageAsync();
        await OpenPlayersAsync(reopened);

        await Assertions.Expect(reopened.GetByLabel("Sort by")).ToHaveValueAsync("LastNameDescending");
        await Assertions.Expect(reopened.Locator(".player-name")).ToHaveTextAsync(["Cat Zed", "Ann Smith", "Bob Adams"]);
    }

    [Test]
    public async Task Players_AfterAppClosedAndReopened_AreStillThere()
    {
        await OpenPlayersAsync();
        await AddPlayerAsync("Ann", "Smith", "3", "Remember me");

        await Page.CloseAsync();
        var reopened = await Context.NewPageAsync();
        await OpenPlayersAsync(reopened);

        await Assertions.Expect(reopened.Locator(".player-name")).ToHaveTextAsync(["Ann Smith"]);
        await reopened.GetByRole(AriaRole.Link, new() { Name = "Edit Ann Smith" }).ClickAsync();
        await Assertions.Expect(reopened.GetByLabel("Comment")).ToHaveValueAsync("Remember me");
        await Assertions.Expect(reopened.GetByLabel("Rank")).ToHaveValueAsync("3");
    }

    [Test]
    public async Task Players_AddedAndReopenedOffline_AreStillThere()
    {
        await Page.GotoAsync(RootUrl());
        await WaitForAppAsync();
        await WaitForOfflineReadyAsync();
        await OpenPlayersAsync();
        await AddPlayerAsync("Ann", "Smith");

        await PublishedSite.Root.StopAsync();
        try
        {
            await Page.CloseAsync();
            var reopened = await Context.NewPageAsync();
            await OpenPlayersAsync(reopened);

            await Assertions.Expect(reopened.Locator(".player-name")).ToHaveTextAsync(["Ann Smith"]);
            await AddPlayerAsync("Bob", "Adams", page: reopened);
            await Assertions.Expect(reopened.Locator(".player-name")).ToHaveTextAsync(["Ann Smith", "Bob Adams"]);
        }
        finally
        {
            await PublishedSite.Root.StartAsync();
        }
    }

    [Test]
    public async Task Players_UnderRepositorySubpath_AreSavedAndListed()
    {
        await Page.GotoAsync(SubUrl("players"));
        await Assertions.Expect(Header).ToHaveTextAsync("Players");

        await AddPlayerAsync("Ann", "Smith");

        await Assertions.Expect(Names).ToHaveTextAsync(["Ann Smith"]);
        Assert.That(Page.Url, Is.EqualTo(SubUrl("players")));
    }

    [TestCase("players")]
    [TestCase("players/add")]
    public async Task PlayerPages_OnIPhoneSe_DoNotScrollHorizontally(string path)
    {
        await Page.GotoAsync(RootUrl(path));
        await WaitForAppAsync();

        var overflow = await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth");

        Assert.That(overflow, Is.False);
    }

    [TestCase(375, 667)]
    [TestCase(430, 932)]
    [TestCase(820, 1180)]
    [TestCase(1280, 800)]
    public async Task Players_AtAnyWidth_FillsTheScreenWithoutScrollingSideways(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await OpenPlayersAsync();
        await AddPlayerAsync("Ann", "Smith");

        var section = await Page.Locator(".page-section").BoundingBoxAsync() ?? throw new InvalidOperationException("No page section.");
        var overflow = await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(section.X, Is.Zero.Within(1));
            Assert.That(section.Width, Is.EqualTo(width).Within(1));
            Assert.That(overflow, Is.False);
        }
    }

    [Test]
    public async Task Header_OnPlayerSubPages_ShowsPlayers()
    {
        await Page.GotoAsync(RootUrl("players/add"));

        await Assertions.Expect(Header).ToHaveTextAsync("Players");
    }
}
