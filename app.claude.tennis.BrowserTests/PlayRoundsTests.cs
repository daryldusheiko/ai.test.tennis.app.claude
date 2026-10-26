using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SocialTennis.BrowserTests;

public partial class PlayRoundsTests : AppTestBase
{
    /// <summary>How far in (of 48) the seams reach at their widest, stroke included.</summary>
    private const double SeamInnerEdge = 12.8;

    private static readonly string[] Names = ["Daryl", "Bob", "Cat", "Dan", "Eve", "Fay", "Gus", "Hal"];

    private ILocator Dialog => Page.GetByRole(AriaRole.Alertdialog);

    private ILocator Card(int round) => Page.Locator(".round-card").Nth(round - 1);

    private ILocator ActionButton(int round) => Card(round).GetByRole(AriaRole.Button, new() { Name = $"Round {round} actions" });

    private static Guid PlayerId(int index) => new($"00000000-0000-0000-0000-{index + 1:D12}");

    private static object Game(int a, int b, int c, int d) =>
        new { Team1 = new[] { PlayerId(a), PlayerId(b) }, Team2 = new[] { PlayerId(c), PlayerId(d) }, IsComplete = false };

    /// <summary>Stores a schedule that is in play on the given courts (eight players, three rounds), straight into browser storage.</summary>
    private async Task SeedInPlayAsync(params int[] courts)
    {
        await Page.GotoAsync(RootUrl());
        await WaitForAppAsync();
        var players = Names.Select((name, index) => new { Id = PlayerId(index), FirstName = name, LastName = index == 0 ? "Dusheiko" : "Test", Rank = (int?)null });
        var rounds = new[]
        {
            new { Games = new[] { Game(0, 1, 2, 3), Game(4, 5, 6, 7) }, SittingOut = Array.Empty<Guid>() },
            new { Games = new[] { Game(0, 2, 4, 6), Game(1, 3, 5, 7) }, SittingOut = Array.Empty<Guid>() },
            new { Games = new[] { Game(0, 4, 1, 5), Game(2, 6, 3, 7) }, SittingOut = Array.Empty<Guid>() },
        };
        var schedule = new { Id = Guid.NewGuid(), Step = "InPlay", Courts = courts, Players = players, Rounds = rounds, Seed = 1 };
        await Page.EvaluateAsync("json => localStorage.setItem('socialTennis.schedule', json)", JsonSerializer.Serialize(schedule));
        await Page.GotoAsync(RootUrl());
        await Assertions.Expect(Page.Locator(".round-card")).ToHaveCountAsync(3);
    }

    private async Task ChooseAsync(int round, string button)
    {
        await ActionButton(round).ClickAsync();
        await Dialog.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync();
        await Assertions.Expect(Dialog).ToHaveCountAsync(0);
    }

    private async Task<string> BallColourAsync(int round, int game = 1) =>
        await Card(round).Locator(".court-ball .ball-fill").Nth(game - 1).EvaluateAsync<string>("element => getComputedStyle(element).fill");

    private static double Luminance(string rgb)
    {
        var channels = DigitsRegex().Matches(rgb).Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture) / 255).Take(3)
            .Select(value => value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4)).ToArray();
        return (0.2126 * channels[0]) + (0.7152 * channels[1]) + (0.0722 * channels[2]);
    }

    private static double ContrastWithWhite(string rgb) => 1.05 / (Luminance(rgb) + 0.05);

    [GeneratedRegex(@"\d+(\.\d+)?")]
    private static partial Regex DigitsRegex();

    [Test]
    public async Task Play_Games_ShowABallWithTheCourtNumberInsteadOfCourtText()
    {
        await SeedInPlayAsync(4, 5);

        await Assertions.Expect(Card(1).GetByRole(AriaRole.Img)).ToHaveCountAsync(2);
        await Assertions.Expect(Card(1).GetByRole(AriaRole.Img, new() { Name = "Court 4" })).ToBeVisibleAsync();
        await Assertions.Expect(Card(1).GetByRole(AriaRole.Img, new() { Name = "Court 5" })).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("Court 4", new() { Exact = true })).ToHaveCountAsync(0);
        await Assertions.Expect(Card(1).Locator(".ball-number").First).ToHaveTextAsync("4");
    }

    [Test]
    public async Task Play_Names_AreFirstNameAndLastInitial()
    {
        await SeedInPlayAsync(4, 5);

        await Assertions.Expect(Card(1).GetByText("Daryl D.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Play_NothingStarted_AllBallsAreBlueAndNoStatusIsShown()
    {
        await SeedInPlayAsync(4, 5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Page.Locator(".court-ball.ball-blue").CountAsync(), Is.EqualTo(6));
            Assert.That(await Page.Locator(".round-status").CountAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task Ball_Colours_GiveTheWhiteNumberAtLeast4Point5ToOne()
    {
        await SeedInPlayAsync(4, 5);
        await ChooseAsync(1, "Start round");
        await ChooseAsync(1, "Finish round");
        await ChooseAsync(2, "Start round");

        var colours = new[] { await BallColourAsync(1), await BallColourAsync(2), await BallColourAsync(3) };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(colours.Distinct().Count(), Is.EqualTo(3), "grey, green and blue must differ");
            foreach (var colour in colours)
            {
                Assert.That(ContrastWithWhite(colour), Is.GreaterThanOrEqualTo(4.5), colour);
            }
        }
    }

    [TestCase(4)]
    [TestCase(12)]
    [TestCase(99)]
    public async Task Ball_Number_IsBoldWhiteWithDarkOutlineAndStaysClearOfTheSeams(int court)
    {
        await SeedInPlayAsync(court, court + 1);
        var number = Card(1).Locator(".ball-number").First;

        var style = await number.EvaluateAsync<string[]>("e => { const s = getComputedStyle(e); return [s.fill, s.stroke, s.fontWeight, s.paintOrder]; }");
        var box = await number.EvaluateAsync<double[]>("e => { const b = e.getBBox(); return [b.x, b.x + b.width, b.height]; }");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(style[0], Is.EqualTo("rgb(255, 255, 255)"));
            Assert.That(style[1], Is.Not.EqualTo("none"));
            Assert.That(int.Parse(style[2], CultureInfo.InvariantCulture), Is.GreaterThanOrEqualTo(700));
            Assert.That(style[3], Does.StartWith("stroke"));
            Assert.That(box[0], Is.GreaterThanOrEqualTo(SeamInnerEdge), "number clears the left seam");
            Assert.That(box[1], Is.LessThanOrEqualTo(48 - SeamInnerEdge), "number clears the right seam");
            Assert.That(box[2], Is.GreaterThanOrEqualTo(15), "number is big");
        }
    }

    [Test]
    public async Task Row_Layout_CentresTheVBetweenThePairsAndEachPairInItsHalf()
    {
        await SeedInPlayAsync(4, 5);
        var row = Card(1).Locator(".game-row").First;

        var rect = await row.EvaluateAsync<double[]>("e => { const r = e.getBoundingClientRect(); return [r.left, r.right]; }");
        var versus = await row.Locator(".versus").BoundingBoxAsync() ?? throw new InvalidOperationException("No v.");
        var teams = await row.Locator(".team").EvaluateAllAsync<double[][]>("els => els.map(e => { const r = e.getBoundingClientRect(); return [r.left, r.right]; })");
        var ball = await row.Locator(".court-ball").BoundingBoxAsync() ?? throw new InvalidOperationException("No ball.");

        var mid = (rect[0] + rect[1]) / 2;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(versus.X + (versus.Width / 2), Is.EqualTo(mid).Within(1.5), "v is in the middle of the row");
            Assert.That(teams[0][1], Is.LessThan(versus.X));
            Assert.That(teams[1][0], Is.GreaterThan(versus.X + versus.Width));
            Assert.That(teams[0][0] - (ball.X + ball.Width), Is.LessThan(30), "no big gap between the ball and the first pair");
            Assert.That(versus.X - teams[0][1], Is.LessThan(40), "the pairs are not bunched against the v nor far from it");
            Assert.That(teams[1][0] - (versus.X + versus.Width), Is.EqualTo(versus.X - teams[0][1]).Within(8), "the pairs sit evenly either side of the v");
            Assert.That(rect[1], Is.LessThanOrEqualTo(Page.ViewportSize!.Width));
        }
    }

    [Test]
    public async Task Round_StartRound_MakesItGreenAndShowsInPlay()
    {
        await SeedInPlayAsync(4, 5);

        await ChooseAsync(2, "Start round");

        await Assertions.Expect(Card(2).Locator(".round-status")).ToHaveTextAsync("In play");
        await Assertions.Expect(Card(2).Locator(".court-ball.ball-green")).ToHaveCountAsync(2);
        await Assertions.Expect(Card(1).Locator(".court-ball.ball-blue")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task Round_AnyOrder_CanStartTheLastRoundFirst()
    {
        await SeedInPlayAsync(4, 5);

        await ChooseAsync(3, "Start round");

        await Assertions.Expect(Card(3).Locator(".court-ball.ball-green")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task Dialog_NotStarted_ShowsTheMessageStartAndClose()
    {
        await SeedInPlayAsync(4, 5);

        await ActionButton(1).ClickAsync();

        await Assertions.Expect(Dialog.GetByRole(AriaRole.Heading, new() { Name = "Round 1" })).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.GetByText("Not started yet.")).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.GetByRole(AriaRole.Button)).ToHaveTextAsync(["Start round", "Close"]);
    }

    [Test]
    public async Task Dialog_AnotherRoundInPlay_DisablesStart()
    {
        await SeedInPlayAsync(4, 5);
        await ChooseAsync(1, "Start round");

        await ActionButton(2).ClickAsync();

        await Assertions.Expect(Dialog.GetByText("Round 1 is in play. Finish or cancel it before starting another.")).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.GetByRole(AriaRole.Button, new() { Name = "Start round" })).ToBeDisabledAsync();
    }

    [Test]
    public async Task Dialog_InPlay_OffersFinishCancelAndClose()
    {
        await SeedInPlayAsync(4, 5);
        await ChooseAsync(1, "Start round");

        await ActionButton(1).ClickAsync();

        await Assertions.Expect(Dialog.GetByText("In play.", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.GetByRole(AriaRole.Button)).ToHaveTextAsync(["Finish round", "Cancel round", "Close"]);
    }

    [Test]
    public async Task Dialog_Close_LeavesTheRoundAsItWas()
    {
        await SeedInPlayAsync(4, 5);

        await ActionButton(1).ClickAsync();
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();

        await Assertions.Expect(Dialog).ToHaveCountAsync(0);
        await Assertions.Expect(Card(1).Locator(".court-ball.ball-blue")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task Round_Finish_MakesItGreyAndDisablesItsActionButton()
    {
        await SeedInPlayAsync(4, 5);
        await ChooseAsync(1, "Start round");

        await ChooseAsync(1, "Finish round");

        await Assertions.Expect(Card(1).Locator(".round-status")).ToHaveTextAsync("Complete");
        await Assertions.Expect(Card(1).Locator(".court-ball.ball-grey")).ToHaveCountAsync(2);
        await Assertions.Expect(ActionButton(1)).ToBeDisabledAsync();
    }

    [Test]
    public async Task Round_FinishThenStartAnother_IsAllowed()
    {
        await SeedInPlayAsync(4, 5);
        await ChooseAsync(1, "Start round");
        await ChooseAsync(1, "Finish round");

        await ChooseAsync(2, "Start round");

        await Assertions.Expect(Card(2).Locator(".court-ball.ball-green")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task Round_Cancel_ReturnsItToNotStarted()
    {
        await SeedInPlayAsync(4, 5);
        await ChooseAsync(1, "Start round");

        await ChooseAsync(1, "Cancel round");

        await Assertions.Expect(Card(1).Locator(".court-ball.ball-blue")).ToHaveCountAsync(2);
        await Assertions.Expect(Card(1).Locator(".round-status")).ToHaveCountAsync(0);
        await Assertions.Expect(ActionButton(1)).ToBeEnabledAsync();
    }

    [Test]
    public async Task Round_StateAfterClosingAndReopeningTheApp_IsKept()
    {
        await SeedInPlayAsync(4, 5);
        await ChooseAsync(1, "Start round");
        await ChooseAsync(1, "Finish round");
        await ChooseAsync(3, "Start round");

        await Page.ReloadAsync();

        await Assertions.Expect(Card(1).Locator(".court-ball.ball-grey")).ToHaveCountAsync(2);
        await Assertions.Expect(Card(3).Locator(".court-ball.ball-green")).ToHaveCountAsync(2);
        await Assertions.Expect(Card(2).Locator(".court-ball.ball-blue")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task Dialog_OnSmallPhoneScrolledDown_IsCentredAndFullyVisible()
    {
        await SeedInPlayAsync(4, 5);
        await ChooseAsync(1, "Start round");
        await Page.EvaluateAsync("document.querySelector('main').scrollTo(0, 100000); window.scrollTo(0, 100000);");

        await ActionButton(3).ScrollIntoViewIfNeededAsync();
        await ActionButton(3).ClickAsync();

        var box = await Page.Locator(".dialog").BoundingBoxAsync() ?? throw new InvalidOperationException("No dialog is shown.");
        var size = Page.ViewportSize ?? throw new InvalidOperationException("No viewport.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(box.X, Is.GreaterThanOrEqualTo(0));
            Assert.That(box.Y, Is.GreaterThanOrEqualTo(0));
            Assert.That(box.X + box.Width, Is.LessThanOrEqualTo(size.Width));
            Assert.That(box.Y + box.Height, Is.LessThanOrEqualTo(size.Height));
            Assert.That(box.Y + (box.Height / 2), Is.EqualTo(size.Height / 2.0).Within(4), "centred, not anchored to the bottom");
            foreach (var button in await Page.Locator(".dialog button").AllAsync())
            {
                var inner = await button.BoundingBoxAsync() ?? throw new InvalidOperationException("A dialog button is hidden.");
                Assert.That(inner.Y + inner.Height, Is.LessThanOrEqualTo(size.Height));
            }
        }
    }

    [Test]
    public async Task Dialog_InAShortWindow_ScrollsInsideItselfWithButtonsReachable()
    {
        await SeedInPlayAsync(4, 5);
        await ChooseAsync(1, "Start round");
        await Page.SetViewportSizeAsync(375, 260);

        await ActionButton(2).ClickAsync();
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Close" }).ScrollIntoViewIfNeededAsync();

        var box = await Page.Locator(".dialog").BoundingBoxAsync() ?? throw new InvalidOperationException("No dialog is shown.");
        var close = await Dialog.GetByRole(AriaRole.Button, new() { Name = "Close" }).BoundingBoxAsync() ?? throw new InvalidOperationException("Close is hidden.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(box.Y + box.Height, Is.LessThanOrEqualTo(260));
            Assert.That(close.Y + close.Height, Is.LessThanOrEqualTo(260));
            Assert.That(close.Y, Is.GreaterThanOrEqualTo(0));
        }
    }
}
