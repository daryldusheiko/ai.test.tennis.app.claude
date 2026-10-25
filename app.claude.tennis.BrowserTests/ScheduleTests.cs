using System.Diagnostics;
using System.Text.Json;
using Microsoft.Playwright;

namespace SocialTennis.BrowserTests;

public class ScheduleTests : AppTestBase
{
    private ILocator Header => Page.GetByRole(AriaRole.Heading, new() { Level = 1 });

    private ILocator Rounds => Page.Locator(".round-card");

    private ILocator Next => Page.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true });

    private static string Name(int index) => $"Pat{index:00}";

    /// <summary>Puts players straight into browser storage, as if they had been added on the Players page.</summary>
    private async Task SeedPlayersAsync(int count, IPage? page = null)
    {
        page ??= Page;
        await page.GotoAsync(RootUrl());
        await WaitForAppAsync(page);
        var players = Enumerable.Range(1, count).Select(i => new
        {
            Id = Guid.NewGuid(),
            FirstName = Name(i),
            LastName = "Test",
            Rank = i % 4 == 0 ? (int?)null : (i % 3) + 1,
            Comment = string.Empty,
        });
        await page.EvaluateAsync("json => localStorage.setItem('socialTennis.players', json)", JsonSerializer.Serialize(players));
    }

    private async Task OpenScheduleAsync(IPage? page = null)
    {
        page ??= Page;
        await page.GotoAsync(RootUrl("schedule"));
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("Schedule");
    }

    private async Task EnterCourtsAsync(string courts, IPage? page = null)
    {
        page ??= Page;
        await page.GetByLabel("Select courts").FillAsync(courts);
        await page.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true }).ClickAsync();
    }

    private async Task AddToScheduleAsync(string search, IPage? page = null)
    {
        page ??= Page;
        await page.GetByLabel("Add players").FillAsync(search);
        await page.GetByLabel("Matching players").GetByRole(AriaRole.Button, new() { Name = search }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Add", Exact = true }).ClickAsync();
    }

    private async Task AddPlayersAsync(int count, IPage? page = null)
    {
        page ??= Page;
        for (var i = 1; i <= count; i++)
        {
            await AddToScheduleAsync($"{Name(i)} Test", page);
        }
    }

    /// <summary>Seeds players, creates a schedule on the given courts using the first players, and waits for the review page.</summary>
    private async Task CreateReviewedScheduleAsync(int players, string courts)
    {
        await SeedPlayersAsync(players);
        await OpenScheduleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create schedule" }).ClickAsync();
        await EnterCourtsAsync(courts);
        await AddPlayersAsync(players);
        await Next.ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Review schedule" })).ToBeVisibleAsync(new() { Timeout = 60_000 });
    }

    private async Task<string[]> FirstGamePerRoundAsync(IPage? page = null)
    {
        page ??= Page;
        var count = await page.Locator(".round-card").CountAsync();
        var texts = new List<string>();
        for (var i = 0; i < count; i++)
        {
            texts.Add(await page.Locator(".round-card").Nth(i).Locator(".game-row").First.InnerTextAsync());
        }

        return [.. texts];
    }

    private static async Task EventuallyAsync(Func<Task> check)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            try
            {
                await check();
                return;
            }
            catch (AssertionException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }
        }
    }

    private static async Task DragAsync(IPage page, ILocator handle, ILocator target)
    {
        var from = await handle.BoundingBoxAsync() ?? throw new InvalidOperationException("The handle is not visible.");
        var to = await target.BoundingBoxAsync() ?? throw new InvalidOperationException("The target is not visible.");
        await page.Mouse.MoveAsync(from.X + (from.Width / 2), from.Y + (from.Height / 2));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(from.X + (from.Width / 2), from.Y + (from.Height / 2) + 5, new() { Steps = 3 });
        await page.Mouse.MoveAsync(to.X + (to.Width / 2), to.Y + (to.Height / 2), new() { Steps = 12 });
        await page.Mouse.UpAsync();
    }

    [Test]
    public async Task Schedule_NoneYet_ShowsCreateScheduleButton()
    {
        await OpenScheduleAsync();

        await Assertions.Expect(Page.GetByText("There is no active schedule.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Create schedule" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task CreateSchedule_Clicked_ShowsTheCourtsStep()
    {
        await OpenScheduleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Create schedule" }).ClickAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Courts" })).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByLabel("Select courts")).ToHaveAttributeAsync("placeholder", "e.g. 4,5,6");
        await Assertions.Expect(Page.GetByText("Enter the court numbers, separated by commas.")).ToBeVisibleAsync();
    }

    [TestCase("", "Enter at least one court number, for example 4,5,6.")]
    [TestCase("4,x", "“x” is not a court number.")]
    [TestCase("0", "Court numbers must be 1 or more.")]
    [TestCase("4,5,4", "Court 4 is listed more than once.")]
    public async Task Courts_BadEntry_ShowsTheMessageAndStays(string courts, string message)
    {
        await OpenScheduleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create schedule" }).ClickAsync();

        await EnterCourtsAsync(courts);

        await Assertions.Expect(Page.GetByRole(AriaRole.Alert)).ToHaveTextAsync(message);
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Courts" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Courts_EnterKey_GoesToThePlayersStep()
    {
        await SeedPlayersAsync(5);
        await OpenScheduleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create schedule" }).ClickAsync();

        await Page.GetByLabel("Select courts").FillAsync("4,5,6");
        await Page.GetByLabel("Select courts").PressAsync("Enter");

        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Players" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task PlayersStep_NoPlayersAtAll_LinksToThePlayersPage()
    {
        await OpenScheduleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create schedule" }).ClickAsync();
        await EnterCourtsAsync("4,5");

        await Assertions.Expect(Page.GetByText("No players yet. Add players first, then come back.")).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Link, new() { Name = "Add players" }).ClickAsync();
        await Assertions.Expect(Header).ToHaveTextAsync("Players");
    }

    [Test]
    public async Task PlayersStep_Typing_SuggestsMatchesFromTheFirstLetterUpToEight()
    {
        await SeedPlayersAsync(12);
        await OpenScheduleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create schedule" }).ClickAsync();
        await EnterCourtsAsync("4,5");

        await Page.GetByLabel("Add players").FillAsync("p");
        var suggestions = Page.GetByLabel("Matching players").GetByRole(AriaRole.Button);
        await Assertions.Expect(suggestions).ToHaveCountAsync(8);

        await Page.GetByLabel("Add players").FillAsync("pat1");
        await Assertions.Expect(suggestions).ToHaveCountAsync(3);

        await Page.GetByLabel("Add players").FillAsync("zzz");
        await Assertions.Expect(Page.GetByText("No matching players.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task PlayersStep_AddAndRemove_ListsFirstAndLastNamesAndNeedsFourToContinue()
    {
        await SeedPlayersAsync(6);
        await OpenScheduleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create schedule" }).ClickAsync();
        await EnterCourtsAsync("4,5");
        await Assertions.Expect(Page.GetByText("Nobody added yet.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("Add at least 4 players.")).ToBeVisibleAsync();
        await Assertions.Expect(Next).ToBeDisabledAsync();

        await AddPlayersAsync(3);
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Playing (3)" })).ToBeVisibleAsync();
        await Assertions.Expect(Next).ToBeDisabledAsync();
        await AddToScheduleAsync($"{Name(4)} Test");

        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Playing (4)" })).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator(".player-name")).ToHaveTextAsync(["Pat01 Test", "Pat02 Test", "Pat03 Test", "Pat04 Test"]);
        await Assertions.Expect(Page.GetByText("Add at least 4 players.")).ToHaveCountAsync(0);
        await Assertions.Expect(Next).ToBeEnabledAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Remove Pat02 Test" }).ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Playing (3)" })).ToBeVisibleAsync();
        await Assertions.Expect(Next).ToBeDisabledAsync();
    }

    [Test]
    public async Task PlayersStep_PlayerAlreadyAdded_IsNotSuggestedAgain()
    {
        await SeedPlayersAsync(3);
        await OpenScheduleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create schedule" }).ClickAsync();
        await EnterCourtsAsync("4,5");
        await AddToScheduleAsync($"{Name(1)} Test");

        await Page.GetByLabel("Add players").FillAsync(Name(1));

        await Assertions.Expect(Page.GetByText("No matching players.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task PlayersStep_Back_SavesTheListAndShowsTheCourtsAgain()
    {
        await SeedPlayersAsync(5);
        await OpenScheduleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create schedule" }).ClickAsync();
        await EnterCourtsAsync("6, 4,5");
        await AddPlayersAsync(2);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Back" }).ClickAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Courts" })).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByLabel("Select courts")).ToHaveValueAsync("4,5,6");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true }).ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Playing (2)" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task PlayersStep_AppClosedAndReopened_ResumesAtTheSameStepWithTheList()
    {
        await SeedPlayersAsync(5);
        await OpenScheduleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create schedule" }).ClickAsync();
        await EnterCourtsAsync("4,5");
        await AddPlayersAsync(2);

        await Page.CloseAsync();
        var reopened = await Context.NewPageAsync();
        await OpenScheduleAsync(reopened);

        await Assertions.Expect(reopened.GetByRole(AriaRole.Heading, new() { Name = "Playing (2)" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task CourtsStep_AppClosedAndReopened_ResumesAtCourts()
    {
        await OpenScheduleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create schedule" }).ClickAsync();

        await Page.CloseAsync();
        var reopened = await Context.NewPageAsync();
        await OpenScheduleAsync(reopened);

        await Assertions.Expect(reopened.GetByRole(AriaRole.Heading, new() { Name = "Courts" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Review_AfterNext_ShowsSixRoundCardsWithGamesAndPairs()
    {
        await CreateReviewedScheduleAsync(8, "4,5");

        await Assertions.Expect(Rounds).ToHaveCountAsync(6);
        await Assertions.Expect(Rounds.Locator("h3")).ToHaveTextAsync(["Round 1", "Round 2", "Round 3", "Round 4", "Round 5", "Round 6"]);
        await Assertions.Expect(Rounds.First.Locator(".game-row")).ToHaveCountAsync(2);
        await Assertions.Expect(Rounds.First.Locator(".game-row").First.Locator(".team")).ToHaveCountAsync(2);
        await Assertions.Expect(Rounds.First.Locator(".game-row").First.Locator(".team").First.Locator(".player-label")).ToHaveCountAsync(2);
        await Assertions.Expect(Page.GetByText("Court 4")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Play", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true })).ToBeDisabledAsync();
    }

    [Test]
    public async Task Review_Names_AreFirstNameAndInitialWithRankDots()
    {
        await CreateReviewedScheduleAsync(8, "4,5");

        await Assertions.Expect(Rounds.First.Locator(".player-label").First).ToContainTextAsync(" T.");
        await Assertions.Expect(Page.Locator(".rank-dot").First).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Img, new() { Name = "Rank 2" }).First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Review_FivePlayers_ExplainsOnlyFiveRoundsAndListsWhoSitsOut()
    {
        await CreateReviewedScheduleAsync(5, "4,5");

        await Assertions.Expect(Rounds).ToHaveCountAsync(5);
        await Assertions.Expect(Page.GetByText("With 5 players only 5 rounds are possible without anyone partnering the same player twice.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("Sitting out:")).ToHaveCountAsync(5);
    }

    [Test]
    public async Task Review_SevenPlayersTwoCourts_ShowsAThreePlayerGame()
    {
        await CreateReviewedScheduleAsync(7, "4,5");

        var oneAgainstTwo = Rounds.First.Locator(".game-row").Nth(1);
        await Assertions.Expect(oneAgainstTwo.Locator(".team").First.Locator(".player-label")).ToHaveCountAsync(1);
        await Assertions.Expect(oneAgainstTwo.Locator(".team").Last.Locator(".player-label")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task Review_DragRoundHandle_ReordersTheRounds()
    {
        await CreateReviewedScheduleAsync(8, "4,5");
        var before = await FirstGamePerRoundAsync();

        await DragAsync(Page, Page.GetByRole(AriaRole.Button, new() { Name = "Move round 1" }), Rounds.Nth(2).Locator("h3"));

        string[] expected = [before[1], before[2], before[0], before[3], before[4], before[5]];
        await EventuallyAsync(async () => Assert.That(await FirstGamePerRoundAsync(), Is.EqualTo(expected)));
        await Assertions.Expect(Rounds.Locator("h3")).ToHaveTextAsync(["Round 1", "Round 2", "Round 3", "Round 4", "Round 5", "Round 6"]);
    }

    [Test]
    public async Task Review_DragGameHandle_ReordersGamesWithinTheRound()
    {
        await CreateReviewedScheduleAsync(8, "4,5");
        var rows = Rounds.Nth(1).Locator(".game-row");
        var first = await rows.Nth(0).InnerTextAsync();
        var second = await rows.Nth(1).InnerTextAsync();

        await DragAsync(Page, Page.GetByRole(AriaRole.Button, new() { Name = "Move game 1 of round 2" }), rows.Nth(1));

        await EventuallyAsync(async () =>
            Assert.That(await rows.AllInnerTextsAsync(), Is.EqualTo(new List<string> { second, first })));
    }

    [Test]
    public async Task Review_ReorderedThenReopened_KeepsTheNewOrder()
    {
        await CreateReviewedScheduleAsync(8, "4,5");
        var before = await FirstGamePerRoundAsync();
        await DragAsync(Page, Page.GetByRole(AriaRole.Button, new() { Name = "Move round 1" }), Rounds.Nth(1).Locator("h3"));
        await EventuallyAsync(async () => Assert.That((await FirstGamePerRoundAsync())[0], Is.EqualTo(before[1])));

        await Page.CloseAsync();
        var reopened = await Context.NewPageAsync();
        await OpenScheduleAsync(reopened);

        await Assertions.Expect(reopened.GetByRole(AriaRole.Heading, new() { Name = "Review schedule" })).ToBeVisibleAsync();
        Assert.That((await FirstGamePerRoundAsync(reopened))[0], Is.EqualTo(before[1]));
    }

    [Test]
    public async Task Review_PlayersLink_ReturnsToThePlayersStepWithTheList()
    {
        await CreateReviewedScheduleAsync(5, "4,5");

        await Page.GetByRole(AriaRole.Link, new() { Name = "‹ Players" }).ClickAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Playing (5)" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Play_ClickedOnReview_ShowsTheScheduleWithCourtNumbersOnTheHomePage()
    {
        await CreateReviewedScheduleAsync(8, "6,4");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Play", Exact = true }).ClickAsync();

        await Assertions.Expect(Header).ToHaveTextAsync("Play");
        await Assertions.Expect(Rounds).ToHaveCountAsync(6);
        await Assertions.Expect(Rounds.First.Locator(".court-label")).ToHaveTextAsync(["Court 4", "Court 6"]);
        await Assertions.Expect(Page.Locator(".drag-handle")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator(".rank-dot")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Play_ThreeCourts_GivesGameOneTheLowestCourt()
    {
        await CreateReviewedScheduleAsync(12, "4,5,6");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Play", Exact = true }).ClickAsync();

        await Assertions.Expect(Rounds.Nth(3).Locator(".court-label")).ToHaveTextAsync(["Court 4", "Court 5", "Court 6"]);
    }

    [Test]
    public async Task Play_ReorderedBeforePlaying_ShowsTheNewOrderWithCourtsInGameOrder()
    {
        await CreateReviewedScheduleAsync(8, "4,5");
        var rows = Rounds.Nth(0).Locator(".game-row");
        var second = await rows.Nth(1).Locator(".team").AllInnerTextsAsync();
        await DragAsync(Page, Page.GetByRole(AriaRole.Button, new() { Name = "Move game 2 of round 1" }), rows.Nth(0));
        await EventuallyAsync(async () => Assert.That(await rows.Nth(0).Locator(".team").AllInnerTextsAsync(), Is.EqualTo(second)));

        await Page.GetByRole(AriaRole.Button, new() { Name = "Play", Exact = true }).ClickAsync();

        var played = Page.Locator(".round-card").First.Locator(".game-row").First;
        await Assertions.Expect(played.Locator(".court-label")).ToHaveTextAsync("Court 4");
        Assert.That(await played.Locator(".team").AllInnerTextsAsync(), Is.EqualTo(second));
    }

    [Test]
    public async Task Play_NoSchedule_SaysToCreateOne()
    {
        await Page.GotoAsync(RootUrl());

        await Assertions.Expect(Page.GetByText("No schedule in play.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("Create one on the Schedule page.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Play_ScheduleNotPlayedYet_SaysToFinishSettingItUp()
    {
        await CreateReviewedScheduleAsync(5, "4,5");

        await Page.GotoAsync(RootUrl());

        await Assertions.Expect(Page.GetByText("No schedule in play.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("Finish setting it up on the Schedule page, then tap Play.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Play_EditButton_OpensTheReviewPageWithoutAPlayersLink()
    {
        await CreateReviewedScheduleAsync(8, "4,5");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Play", Exact = true }).ClickAsync();

        await Page.GetByRole(AriaRole.Link, new() { Name = "Edit", Exact = true }).ClickAsync();

        await Assertions.Expect(Header).ToHaveTextAsync("Schedule");
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Review schedule" })).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Link, new() { Name = "‹ Players" })).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Play_AppClosedAndReopened_StillShowsTheSchedule()
    {
        await CreateReviewedScheduleAsync(8, "4,5");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Play", Exact = true }).ClickAsync();
        var texts = await Page.Locator(".round-card").AllInnerTextsAsync();

        await Page.CloseAsync();
        var reopened = await Context.NewPageAsync();
        await reopened.GotoAsync(RootUrl());

        await Assertions.Expect(reopened.Locator(".round-card")).ToHaveCountAsync(6);
        Assert.That(await reopened.Locator(".round-card").AllInnerTextsAsync(), Is.EqualTo(texts));
    }

    [Test]
    public async Task Schedule_CreatedAndPlayedOffline_Works()
    {
        await SeedPlayersAsync(8);
        await OpenScheduleAsync();
        await WaitForOfflineReadyAsync();

        await PublishedSite.Root.StopAsync();
        try
        {
            await Page.GetByRole(AriaRole.Button, new() { Name = "Create schedule" }).ClickAsync();
            await EnterCourtsAsync("1,2");
            await AddPlayersAsync(8);
            await Next.ClickAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Review schedule" })).ToBeVisibleAsync(new() { Timeout = 60_000 });
            await Page.GetByRole(AriaRole.Button, new() { Name = "Play", Exact = true }).ClickAsync();
            await Page.ReloadAsync();

            await Assertions.Expect(Rounds).ToHaveCountAsync(6);
        }
        finally
        {
            await PublishedSite.Root.StartAsync();
        }
    }

    [Test]
    public async Task Review_OnIPhoneSe_DoesNotScrollSideways()
    {
        await CreateReviewedScheduleAsync(8, "4,5");

        var overflow = await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth || document.querySelector('.app-content').scrollWidth > document.querySelector('.app-content').clientWidth");

        Assert.That(overflow, Is.False);
    }

    [Test]
    public async Task Review_LongSchedule_ScrollsInsideThePageWithToolbarStillOnScreen()
    {
        await CreateReviewedScheduleAsync(8, "4,5");

        var toolbar = await Page.GetByRole(AriaRole.Navigation, new() { Name = "Main" }).BoundingBoxAsync() ?? throw new InvalidOperationException("No toolbar.");
        var viewport = Page.ViewportSize ?? throw new InvalidOperationException("No viewport.");

        Assert.That(toolbar.Y + toolbar.Height, Is.EqualTo(viewport.Height).Within(1));
        await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Play", Exact = true })).Not.ToBeInViewportAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Play", Exact = true }).ScrollIntoViewIfNeededAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Play", Exact = true })).ToBeInViewportAsync();
    }

    [Test]
    public async Task Generate_TwentyFourPlayersOnSixCourts_IsQuickEnoughAndShowsMatchingPlayers()
    {
        await SeedPlayersAsync(24);
        await OpenScheduleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create schedule" }).ClickAsync();
        await EnterCourtsAsync("1,2,3,4,5,6");
        await AddPlayersAsync(24);
        var matching = Page.GetByText("Matching players…");
        var sawMatching = Page.WaitForFunctionAsync("() => document.body.innerText.includes('Matching players…')");

        var watch = Stopwatch.StartNew();
        await Next.ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Review schedule" })).ToBeVisibleAsync(new() { Timeout = 60_000 });
        watch.Stop();

        await sawMatching;
        await Assertions.Expect(matching).ToHaveCountAsync(0);
        await Assertions.Expect(Rounds).ToHaveCountAsync(6);
        await Assertions.Expect(Rounds.First.Locator(".game-row")).ToHaveCountAsync(6);
        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(15)));
    }
}
