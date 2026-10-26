using System.Text.Json;
using Microsoft.Playwright;

namespace SocialTennis.BrowserTests;

public class EditScheduleTests : AppTestBase
{
    private static readonly string[] Names = ["Ann", "Bob", "Cat", "Dan", "Eve", "Fay", "Gus", "Hal"];

    private ILocator Dialog => Page.GetByRole(AriaRole.Alertdialog);

    private ILocator Banner => Page.Locator(".edit-banner");

    private static Guid PlayerId(int index) => new($"00000000-0000-0000-0000-{index + 1:D12}");

    private static object Game(int a, int b, int c, int d, bool complete) =>
        new { Team1 = new[] { PlayerId(a), PlayerId(b) }, Team2 = new[] { PlayerId(c), PlayerId(d) }, IsComplete = complete };

    /// <summary>
    /// Stores a reviewed schedule of eight players on courts 1 and 2 straight into browser storage, so the rounds are known:
    /// round 1 Ann+Bob v Cat+Dan, Eve+Fay v Gus+Hal; round 2 Ann+Cat v Eve+Gus, Bob+Dan v Fay+Hal; round 3 Ann+Eve v Bob+Fay, Cat+Gus v Dan+Hal.
    /// </summary>
    private async Task SeedScheduleAsync(bool firstRoundComplete = false)
    {
        await Page.GotoAsync(RootUrl());
        await WaitForAppAsync();
        var players = Names.Select((name, index) => new { Id = PlayerId(index), FirstName = name, LastName = "Test", Rank = (int?)null });
        var rounds = new[]
        {
            new { Games = new[] { Game(0, 1, 2, 3, firstRoundComplete), Game(4, 5, 6, 7, firstRoundComplete) }, SittingOut = Array.Empty<Guid>() },
            new { Games = new[] { Game(0, 2, 4, 6, false), Game(1, 3, 5, 7, false) }, SittingOut = Array.Empty<Guid>() },
            new { Games = new[] { Game(0, 4, 1, 5, false), Game(2, 6, 3, 7, false) }, SittingOut = Array.Empty<Guid>() },
        };
        var schedule = new { Id = Guid.NewGuid(), Step = "Review", Courts = new[] { 1, 2 }, Players = players, Rounds = rounds, Seed = 1 };
        await Page.EvaluateAsync("json => localStorage.setItem('socialTennis.schedule', json)", JsonSerializer.Serialize(schedule));
        await Page.GotoAsync(RootUrl("schedule"));
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Review schedule" })).ToBeVisibleAsync();
    }

    private ILocator NameIn(int round, string name) =>
        Page.Locator(".round-card").Nth(round - 1).GetByRole(AriaRole.Button, new() { Name = $"{name} T." });

    private ILocator Button(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    private async Task StartEditingAsync()
    {
        await Button("Edit").ClickAsync();
        await Assertions.Expect(Page.GetByText("Press and hold a name to pin or swap a player.")).ToBeVisibleAsync();
    }

    /// <summary>A real press-and-hold with the pointer, held well past the half second the app waits for.</summary>
    private async Task HoldAsync(ILocator target)
    {
        await target.ScrollIntoViewIfNeededAsync();
        var box = await target.BoundingBoxAsync() ?? throw new InvalidOperationException("The name is not visible.");
        await Page.Mouse.MoveAsync(box.X + (box.Width / 2), box.Y + (box.Height / 2));
        await Page.Mouse.DownAsync();
        await Task.Delay(800);
        await Page.Mouse.UpAsync();
    }

    private async Task SwapAsync(int round, string first, string second)
    {
        await HoldAsync(NameIn(round, first));
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Swap", Exact = true }).ClickAsync();
        await NameIn(round, second).ClickAsync();
    }

    private async Task AssertDialogFitsAsync()
    {
        var box = await Page.Locator(".dialog").BoundingBoxAsync() ?? throw new InvalidOperationException("No dialog is shown.");
        var size = Page.ViewportSize ?? throw new InvalidOperationException("No viewport.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(box.X, Is.GreaterThanOrEqualTo(0));
            Assert.That(box.Y, Is.GreaterThanOrEqualTo(0));
            Assert.That(box.X + box.Width, Is.LessThanOrEqualTo(size.Width));
            Assert.That(box.Y + box.Height, Is.LessThanOrEqualTo(size.Height));
        }

        foreach (var button in await Page.Locator(".dialog button").AllAsync())
        {
            var inner = await button.BoundingBoxAsync() ?? throw new InvalidOperationException("A dialog button is hidden.");
            Assert.That(inner.Y + inner.Height, Is.LessThanOrEqualTo(size.Height));
        }
    }

    [Test]
    public async Task Edit_Clicked_ShowsTheBannerAndHidesTheDragHandles()
    {
        await SeedScheduleAsync();
        await Assertions.Expect(Page.Locator(".drag-handle").First).ToBeVisibleAsync();

        await StartEditingAsync();

        await Assertions.Expect(Page.Locator(".drag-handle")).ToHaveCountAsync(0);
        await Assertions.Expect(Button("Done")).ToBeVisibleAsync();
        await Assertions.Expect(Button("Re-schedule")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Done_Clicked_ReturnsToReviewWithDragHandles()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();

        await Button("Done").ClickAsync();

        await Assertions.Expect(Page.Locator(".drag-handle").First).ToBeVisibleAsync();
        await Assertions.Expect(Banner).ToHaveCountAsync(0);
    }

    [Test]
    public async Task PressAndHold_PlayerName_OpensTheOptionsDialog()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();

        await HoldAsync(NameIn(2, "Cat"));

        await Assertions.Expect(Dialog.GetByRole(AriaRole.Heading, new() { Name = "Cat Test" })).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.GetByText("Round 2.")).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.GetByRole(AriaRole.Button)).ToHaveTextAsync(["Pin", "Swap", "Cancel"]);
    }

    [Test]
    public async Task PressAndHold_ReleasedEarly_DoesNothing()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();

        await NameIn(2, "Cat").ClickAsync();

        await Assertions.Expect(Dialog).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Enter_OnFocusedName_OpensTheOptionsDialog()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();

        await NameIn(2, "Cat").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(Dialog.GetByRole(AriaRole.Heading, new() { Name = "Cat Test" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Pin_FromTheOptionsDialog_ShowsAPinAndOffersUnpin()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();
        await HoldAsync(NameIn(2, "Cat"));

        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Pin", Exact = true }).ClickAsync();

        await Assertions.Expect(Dialog).ToHaveCountAsync(0);
        await Assertions.Expect(Banner.GetByText("Pinned Cat T.")).ToBeVisibleAsync();
        await Assertions.Expect(NameIn(2, "Cat").GetByRole(AriaRole.Img, new() { Name = "Pinned" })).ToBeVisibleAsync();
        await HoldAsync(NameIn(2, "Cat"));
        await Assertions.Expect(Dialog.GetByText("Pinned in this game: re-scheduling won't move them and they can't be swapped.")).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.GetByRole(AriaRole.Button)).ToHaveTextAsync(["Unpin", "Cancel"]);
    }

    [Test]
    public async Task Unpin_PinnedPlayer_RemovesThePin()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();
        await HoldAsync(NameIn(2, "Cat"));
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Pin", Exact = true }).ClickAsync();
        await HoldAsync(NameIn(2, "Cat"));

        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Unpin" }).ClickAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Img, new() { Name = "Pinned" })).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Pin_ThenReload_KeepsThePin()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();
        await HoldAsync(NameIn(2, "Cat"));
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Pin", Exact = true }).ClickAsync();

        await Page.ReloadAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Img, new() { Name = "Pinned" })).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Swap_TwoPlayersInARound_ExchangesThemAndShowsThemInBlue()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();
        await HoldAsync(NameIn(3, "Ann"));

        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Swap", Exact = true }).ClickAsync();

        await Assertions.Expect(Banner.GetByText("Tap a player in round 3 to swap with Ann T.")).ToBeVisibleAsync();
        await Assertions.Expect(Banner.GetByRole(AriaRole.Button, new() { Name = "Cancel" })).ToBeVisibleAsync();
        await NameIn(3, "Cat").ClickAsync();
        await Assertions.Expect(Banner.GetByText("Swapped Ann T. and Cat T.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator(".round-card").Nth(2).Locator(".game-row").First).ToContainTextAsync("Cat T.");
        await Assertions.Expect(Page.Locator(".round-card").Nth(2).Locator(".mark-changed")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task Swap_Cancelled_ChangesNothing()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();
        await HoldAsync(NameIn(3, "Ann"));
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Swap", Exact = true }).ClickAsync();

        await Banner.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();

        await Assertions.Expect(Banner.GetByText("Press and hold a name to pin or swap a player.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator(".mark-changed")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Swap_PlayerInAnotherRound_ShowsAMessageAndKeepsWaiting()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();
        await HoldAsync(NameIn(3, "Ann"));
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Swap", Exact = true }).ClickAsync();

        await NameIn(2, "Cat").ClickAsync();

        await Assertions.Expect(Banner.GetByText("Pick a player in round 3.")).ToBeVisibleAsync();
        await Assertions.Expect(Banner.GetByText("Tap a player in round 3 to swap with Ann T.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Swap_PlayersOnTheSameSide_ShowsAMessage()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();

        await SwapAsync(3, "Ann", "Eve");

        await Assertions.Expect(Banner.GetByText("Ann T. and Eve T. are already on the same side.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Swap_CreatingASecondPartnership_ShowsTheirNamesInRed()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();

        await SwapAsync(2, "Bob", "Cat");

        await Assertions.Expect(Banner.GetByText("Partners who play together twice are shown in red.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator(".mark-repeat")).ToHaveCountAsync(8);
    }

    [Test]
    public async Task Reschedule_RedPartners_SeparatesThemAndShowsWhoMovedInBlue()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();
        await SwapAsync(2, "Bob", "Cat");
        await Assertions.Expect(Page.Locator(".mark-repeat")).ToHaveCountAsync(8);

        await Button("Re-schedule").ClickAsync();

        await Assertions.Expect(Banner.GetByText("Re-scheduled:")).ToContainTextAsync("players moved (shown in blue).");
        await Assertions.Expect(Page.Locator(".mark-repeat")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator(".mark-changed").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Reschedule_NothingRed_SaysNothingNeededChanging()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();

        await Button("Re-schedule").ClickAsync();

        await Assertions.Expect(Banner.GetByText("Nothing needed changing.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Done_AfterEdits_ClearsTheBlueMarkers()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();
        await SwapAsync(3, "Ann", "Cat");
        await Assertions.Expect(Page.Locator(".mark-changed")).ToHaveCountAsync(2);

        await Button("Done").ClickAsync();

        await Assertions.Expect(Page.Locator(".mark-changed")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Play_AfterEdits_ShowsThePinnedPlayerAndNoBlueOnThePlayPage()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();
        await HoldAsync(NameIn(2, "Cat"));
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Pin", Exact = true }).ClickAsync();
        await SwapAsync(3, "Ann", "Cat");

        await Button("Play").ClickAsync();

        await Assertions.Expect(Page.Locator("h1#page-title")).ToHaveTextAsync("Play");
        await Assertions.Expect(Page.GetByRole(AriaRole.Img, new() { Name = "Pinned" })).ToHaveCountAsync(1);
        await Assertions.Expect(Page.Locator(".mark-changed")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Cat T." })).ToHaveCountAsync(0);
    }

    [Test]
    public async Task PressAndHold_PlayerInCompletedRound_OffersOnlyClose()
    {
        await SeedScheduleAsync(firstRoundComplete: true);
        await StartEditingAsync();

        await HoldAsync(NameIn(1, "Ann"));

        await Assertions.Expect(Dialog.GetByText("Round 1 is complete, so nothing in it can be changed.")).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.GetByRole(AriaRole.Button)).ToHaveTextAsync(["Close"]);
    }

    [Test]
    public async Task Reschedule_RepeatOfACompletedGame_AsksWhoShouldMove()
    {
        await SeedScheduleAsync(firstRoundComplete: true);
        await StartEditingAsync();
        await SwapAsync(2, "Bob", "Cat");

        await Button("Re-schedule").ClickAsync();

        await Assertions.Expect(Dialog.GetByRole(AriaRole.Heading, new() { Name = "Already played together" })).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.GetByText("were partners in round 1, which is complete, and are partners again in round 2. Who should move?")).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.GetByRole(AriaRole.Button)).ToHaveTextAsync(["Move Ann", "Move Bob", "Keep them together", "Cancel"]);
        await AssertDialogFitsAsync();
    }

    [Test]
    public async Task AlreadyPlayedTogether_MoveChosen_SeparatesThemAndAsksAboutTheNextPair()
    {
        await SeedScheduleAsync(firstRoundComplete: true);
        await StartEditingAsync();
        await SwapAsync(2, "Bob", "Cat");
        await Button("Re-schedule").ClickAsync();

        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Move Bob" }).ClickAsync();

        await Assertions.Expect(Dialog.GetByRole(AriaRole.Heading, new() { Name = "Already played together" })).ToBeVisibleAsync();
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Keep them together" }).ClickAsync();
        await Assertions.Expect(Dialog).ToHaveCountAsync(0);
    }

    [Test]
    public async Task AlreadyPlayedTogether_KeepThemTogether_RemovesTheRed()
    {
        await SeedScheduleAsync(firstRoundComplete: true);
        await StartEditingAsync();
        await SwapAsync(2, "Bob", "Cat");
        await Assertions.Expect(Page.Locator(".mark-repeat")).Not.ToHaveCountAsync(0);
        await Button("Re-schedule").ClickAsync();

        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Keep them together" }).ClickAsync();
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Keep them together" }).ClickAsync();

        await Assertions.Expect(Dialog).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator(".mark-repeat")).ToHaveCountAsync(0);
        await Button("Re-schedule").ClickAsync();
        await Assertions.Expect(Banner.GetByText("Nothing needed changing.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task AlreadyPlayedTogether_Cancelled_ChangesNothing()
    {
        await SeedScheduleAsync(firstRoundComplete: true);
        await StartEditingAsync();
        await SwapAsync(2, "Bob", "Cat");
        await Button("Re-schedule").ClickAsync();

        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();

        await Assertions.Expect(Dialog).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator(".mark-repeat")).Not.ToHaveCountAsync(0);
    }

    [Test]
    public async Task OptionsDialog_OnSmallPhone_FitsTheScreenWhereverThePageIsScrolled()
    {
        await SeedScheduleAsync();
        await StartEditingAsync();
        await Page.Locator(".round-card").Last.ScrollIntoViewIfNeededAsync();
        await Page.EvaluateAsync("document.querySelector('main').scrollTo(0, document.querySelector('main').scrollHeight)");

        await HoldAsync(NameIn(3, "Hal"));

        await Assertions.Expect(Dialog).ToBeVisibleAsync();
        await AssertDialogFitsAsync();
    }

    [Test]
    public async Task Edit_WithTheServerUnreachable_StillWorks()
    {
        await SeedScheduleAsync();
        await WaitForOfflineReadyAsync();

        await PublishedSite.Root.StopAsync();
        try
        {
            await Page.ReloadAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Review schedule" })).ToBeVisibleAsync();

            await StartEditingAsync();
            await HoldAsync(NameIn(2, "Cat"));

            await Assertions.Expect(Dialog.GetByRole(AriaRole.Heading, new() { Name = "Cat Test" })).ToBeVisibleAsync();
        }
        finally
        {
            await PublishedSite.Root.StartAsync();
        }
    }
}
