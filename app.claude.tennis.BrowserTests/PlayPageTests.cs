using System.Text.Json;
using Microsoft.Playwright;

namespace SocialTennis.BrowserTests;

public class PlayPageTests : AppTestBase
{
    private static readonly string[] Names = ["Ann", "Bob", "Cat", "Dan", "Eve", "Fay", "Gus", "Hal"];

    private ILocator Header => Page.GetByRole(AriaRole.Heading, new() { Level = 1 });

    private ILocator Toolbar => Page.GetByRole(AriaRole.Navigation, new() { Name = "Main" });

    private ILocator Dialog => Page.GetByRole(AriaRole.Alertdialog);

    private static Guid PlayerId(int index) => new($"00000000-0000-0000-0000-{index + 1:D12}");

    private static object Game(int a, int b, int c, int d) =>
        new { Team1 = new[] { PlayerId(a), PlayerId(b) }, Team2 = new[] { PlayerId(c), PlayerId(d) }, IsComplete = false };

    private async Task StoreScheduleAsync(string step)
    {
        await Page.GotoAsync(RootUrl("about"));
        await WaitForAppAsync();
        var players = Names.Select((name, index) => new { Id = PlayerId(index), FirstName = name, LastName = "Test", Rank = index < 3 ? (int?)(index + 1) : null });
        var rounds = new[]
        {
            new { Games = new[] { Game(0, 1, 2, 3), Game(4, 5, 6, 7) }, SittingOut = Array.Empty<Guid>() },
            new { Games = new[] { Game(0, 2, 4, 6), Game(1, 3, 5, 7) }, SittingOut = Array.Empty<Guid>() },
        };
        var schedule = new { Id = Guid.NewGuid(), Step = step, Courts = new[] { 4, 5 }, Players = players, Rounds = rounds, Seed = 1 };
        await Page.EvaluateAsync("json => localStorage.setItem('socialTennis.schedule', json)", JsonSerializer.Serialize(schedule));
    }

    private async Task OpenPlayPageWithScheduleInPlayAsync()
    {
        await StoreScheduleAsync("InPlay");
        await Page.GotoAsync(RootUrl());
        await Assertions.Expect(Page.Locator(".round-card")).ToHaveCountAsync(2);
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
    }

    [Test]
    public async Task Start_NothingInPlay_OpensOnPlayers()
    {
        await Page.GotoAsync(RootUrl());

        await Assertions.Expect(Header).ToHaveTextAsync("Players");
        Assert.That(Page.Url, Is.EqualTo(RootUrl("players")));
    }

    [Test]
    public async Task Start_ScheduleStillBeingSetUp_OpensOnPlayers()
    {
        await StoreScheduleAsync("Review");

        await Page.GotoAsync(RootUrl());

        await Assertions.Expect(Header).ToHaveTextAsync("Players");
    }

    [Test]
    public async Task Start_ScheduleInPlay_StaysOnPlay()
    {
        await OpenPlayPageWithScheduleInPlayAsync();

        await Assertions.Expect(Header).ToHaveTextAsync("Play");
    }

    [Test]
    public async Task Start_OnPlayersThenPlayTapped_StaysOnPlayWithTheNoScheduleMessages()
    {
        await Page.GotoAsync(RootUrl());
        await Assertions.Expect(Header).ToHaveTextAsync("Players");

        await Toolbar.GetByRole(AriaRole.Link, new() { Name = "Play", Exact = true }).ClickAsync();

        await Assertions.Expect(Header).ToHaveTextAsync("Play");
        await Assertions.Expect(Page.GetByText("No schedule in play.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("Create one on the Schedule page.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Start_OpenedOnAnotherPageThenPlayTapped_StaysOnPlay()
    {
        await Page.GotoAsync(RootUrl("about"));
        await Assertions.Expect(Header).ToHaveTextAsync("About");

        await Toolbar.GetByRole(AriaRole.Link, new() { Name = "Play", Exact = true }).ClickAsync();

        await Assertions.Expect(Header).ToHaveTextAsync("Play");
        await Assertions.Expect(Page.GetByText("No schedule in play.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Play_ScheduleBeingSetUp_ExplainsToFinishSettingItUp()
    {
        await StoreScheduleAsync("Review");
        await Page.GotoAsync(RootUrl("about"));
        await Toolbar.GetByRole(AriaRole.Link, new() { Name = "Play", Exact = true }).ClickAsync();

        await Assertions.Expect(Page.GetByText("No schedule in play.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("Finish setting it up on the Schedule page, then tap Play.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Play_ScheduleInPlay_HasNoEditButtonOrLinkToEditing()
    {
        await OpenPlayPageWithScheduleInPlayAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Edit" })).ToHaveCountAsync(0);
        await Assertions.Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Edit" })).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator(".app-content a[href='schedule']")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Play_ScheduleInPlay_ShowsNoRankDots()
    {
        await OpenPlayPageWithScheduleInPlayAsync();

        await Assertions.Expect(Page.Locator(".rank-dot")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Play_Names_AreLeftJustifiedWithinEachSide()
    {
        await OpenPlayPageWithScheduleInPlayAsync();

        foreach (var team in await Page.Locator(".round-card").First.Locator(".team").AllAsync())
        {
            var lefts = new List<double>();
            foreach (var label in await team.Locator(".player-label").AllAsync())
            {
                lefts.Add((await label.BoundingBoxAsync())!.X);
            }

            Assert.That(lefts.Max() - lefts.Min(), Is.LessThan(1), "names in a side start at the same left edge");
            var alignment = await team.EvaluateAsync<string>("element => getComputedStyle(element).textAlign");
            Assert.That(alignment, Is.EqualTo("left"));
        }
    }

    [Test]
    public async Task CloseSchedule_InPlay_SitsUnderTheCards()
    {
        await OpenPlayPageWithScheduleInPlayAsync();

        var button = await Page.GetByRole(AriaRole.Button, new() { Name = "Close schedule", Exact = true }).BoundingBoxAsync();
        var lastCard = await Page.Locator(".round-card").Last.BoundingBoxAsync();

        Assert.That(button!.Y, Is.GreaterThan(lastCard!.Y + lastCard.Height - 1));
    }

    [Test]
    public async Task CloseSchedule_Clicked_AsksForConfirmationAndFitsTheScreen()
    {
        await OpenPlayPageWithScheduleInPlayAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Close schedule", Exact = true }).ClickAsync();

        await Assertions.Expect(Dialog.GetByRole(AriaRole.Heading, new() { Name = "Close this schedule?" })).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.GetByText("The schedule and all its rounds will be deleted. Your players are kept. This can't be undone.")).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.GetByRole(AriaRole.Button)).ToHaveTextAsync(["Cancel", "Close schedule"]);
        await AssertDialogFitsAsync();
    }

    [Test]
    public async Task CloseSchedule_Cancelled_KeepsTheSchedule()
    {
        await OpenPlayPageWithScheduleInPlayAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Close schedule", Exact = true }).ClickAsync();

        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();

        await Assertions.Expect(Dialog).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator(".round-card")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task CloseSchedule_Confirmed_DeletesTheScheduleAndTheSchedulePageOffersCreate()
    {
        await OpenPlayPageWithScheduleInPlayAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Close schedule", Exact = true }).ClickAsync();

        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Close schedule", Exact = true }).ClickAsync();

        await Assertions.Expect(Page.GetByText("No schedule in play.")).ToBeVisibleAsync();
        await Toolbar.GetByRole(AriaRole.Link, new() { Name = "Schedule", Exact = true }).ClickAsync();
        await Assertions.Expect(Page.GetByText("There is no active schedule.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Create schedule", Exact = true })).ToBeVisibleAsync();
        Assert.That(await Page.EvaluateAsync<string?>("localStorage.getItem('socialTennis.schedule')"), Is.Empty.Or.Null);
    }

    [Test]
    public async Task CloseSchedule_Confirmed_KeepsThePlayers()
    {
        await OpenPlayPageWithScheduleInPlayAsync();
        await Page.EvaluateAsync("json => localStorage.setItem('socialTennis.players', json)", "[{\"Id\":\"" + Guid.NewGuid() + "\",\"FirstName\":\"Ann\",\"LastName\":\"Smith\",\"Rank\":null,\"Comment\":\"\"}]");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Close schedule", Exact = true }).ClickAsync();
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Close schedule", Exact = true }).ClickAsync();

        await Toolbar.GetByRole(AriaRole.Link, new() { Name = "Players", Exact = true }).ClickAsync();

        await Assertions.Expect(Page.Locator(".player-name")).ToHaveTextAsync(["Ann Smith"]);
    }

    [Test]
    public async Task CloseSchedule_ThenReopeningTheApp_StartsOnPlayers()
    {
        await OpenPlayPageWithScheduleInPlayAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Close schedule", Exact = true }).ClickAsync();
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Close schedule", Exact = true }).ClickAsync();
        await Assertions.Expect(Page.GetByText("No schedule in play.")).ToBeVisibleAsync();

        await Page.GotoAsync(RootUrl());

        await Assertions.Expect(Header).ToHaveTextAsync("Players");
    }
}
