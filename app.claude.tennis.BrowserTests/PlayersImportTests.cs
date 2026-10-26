using Microsoft.Playwright;

namespace SocialTennis.BrowserTests;

public class PlayersImportTests : AppTestBase
{
    private const string Placeholder = "One player per line, e.g.\nDaryl, Smith\n\"Mary Ann\", \"Jones\"\nSam, Lee, 2\n\"Jo\", \"De Silva\", \"3\"";

    private ILocator Dialog => Page.GetByRole(AriaRole.Dialog, new() { Name = "Import players" });

    private ILocator Text => Dialog.GetByLabel("Players to import");

    private ILocator Save => Dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true });

    private ILocator Names => Page.Locator(".player-name");

    private async Task OpenImportAsync()
    {
        await Page.GotoAsync(RootUrl("players"));
        await Page.GetByRole(AriaRole.Button, new() { Name = "Import", Exact = true }).ClickAsync();
        await Assertions.Expect(Dialog).ToBeVisibleAsync();
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
    public async Task Import_Clicked_OpensTheDialogWithHelpTextAndPlaceholder()
    {
        await OpenImportAsync();

        await Assertions.Expect(Dialog.GetByRole(AriaRole.Heading, new() { Name = "Import players" })).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.GetByText("One player per line: first name, last name and an optional rank (1–3), separated by commas.")).ToBeVisibleAsync();
        await Assertions.Expect(Text).ToHaveAttributeAsync("placeholder", Placeholder);
        await Assertions.Expect(Save).ToBeDisabledAsync();
        await Assertions.Expect(Dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true })).ToBeEnabledAsync();
    }

    [Test]
    public async Task Import_DialogOnIPhoneSe_IsFullyVisibleEmptyAndWithErrors()
    {
        await OpenImportAsync();
        await AssertDialogFitsAsync();

        await Text.FillAsync("a\nb\nc\nd\ne\nf\ng\nh");

        await Assertions.Expect(Dialog.GetByText("…and 3 more.")).ToBeVisibleAsync();
        await AssertDialogFitsAsync();
        var heights = await Page.Locator(".dialog").EvaluateAsync<int[]>("element => [element.scrollHeight, element.clientHeight]");
        Assert.That(heights[0], Is.LessThanOrEqualTo(heights[1]), "title, message and every button are visible without scrolling");
    }

    [Test]
    public async Task Import_DialogInAVeryShortWindow_ScrollsInsideItselfWithEveryButtonReachable()
    {
        await Page.SetViewportSizeAsync(375, 360);
        await OpenImportAsync();
        await Text.FillAsync("a\nb\nc");

        var box = await Page.Locator(".dialog").BoundingBoxAsync() ?? throw new InvalidOperationException("No dialog is shown.");
        await Save.ScrollIntoViewIfNeededAsync();

        Assert.That(box.Y + box.Height, Is.LessThanOrEqualTo(360));
        await Assertions.Expect(Save).ToBeInViewportAsync();
        await Assertions.Expect(Dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true })).ToBeInViewportAsync();
    }

    [Test]
    public async Task Import_EveryFormat_ShowsHowManyPlayersAreReady()
    {
        await OpenImportAsync();

        await Text.FillAsync("Daryl, Smith\n\"Mary Ann\", \"Jones\"\nSam, Lee, 2\n\"Jo\", \"De Silva\", \"3\"");

        await Assertions.Expect(Dialog.GetByText("4 players ready to import.")).ToBeVisibleAsync();
        await Assertions.Expect(Save).ToBeEnabledAsync();
        await Assertions.Expect(Dialog.Locator("mark")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Import_OnePlayer_SaysPlayerInTheSingular()
    {
        await OpenImportAsync();

        await Text.FillAsync("Sam, Lee");

        await Assertions.Expect(Dialog.GetByText("1 player ready to import.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Save_ValidPlayers_AddsThemClosesTheDialogAndTellsTheUser()
    {
        await OpenImportAsync();
        await Text.FillAsync("Daryl, Smith\n\"Mary Ann\", \"Jones\"\nSam, Lee, 2");

        await Save.ClickAsync();

        await Assertions.Expect(Dialog).ToHaveCountAsync(0);
        await Assertions.Expect(Page.GetByText("Imported 3 players.")).ToBeVisibleAsync();
        await Assertions.Expect(Names).ToHaveTextAsync(["Daryl Smith", "Mary Ann Jones", "Sam Lee"]);
    }

    [Test]
    public async Task Save_ImportedPlayers_AreStillThereAfterReopening()
    {
        await OpenImportAsync();
        await Text.FillAsync("Sam, Lee, 2");
        await Save.ClickAsync();
        await Assertions.Expect(Names).ToHaveTextAsync(["Sam Lee"]);

        await Page.ReloadAsync();

        await Assertions.Expect(Names).ToHaveTextAsync(["Sam Lee"]);
        await Page.GetByRole(AriaRole.Link, new() { Name = "Edit Sam Lee" }).ClickAsync();
        await Assertions.Expect(Page.GetByLabel("Rank")).ToHaveValueAsync("2");
    }

    [Test]
    public async Task Save_PlayersAlreadyInTheList_AreSkippedAndCounted()
    {
        await OpenImportAsync();
        await Text.FillAsync("Ann, Smith");
        await Save.ClickAsync();
        await Assertions.Expect(Names).ToHaveTextAsync(["Ann Smith"]);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Import", Exact = true }).ClickAsync();
        await Text.FillAsync("ANN, smith\nBob, Jones");
        await Save.ClickAsync();

        await Assertions.Expect(Page.GetByText("Imported 1 players (1 already here).")).ToBeVisibleAsync();
        await Assertions.Expect(Names).ToHaveTextAsync(["Ann Smith", "Bob Jones"]);
    }

    [Test]
    public async Task Import_UnclosedQuote_UnderlinesItListsTheErrorAndBlocksSave()
    {
        await OpenImportAsync();

        await Text.FillAsync("Ann, Smith\nBob, \"Jo");

        await Assertions.Expect(Dialog.GetByText("Line 2: a quote is not closed.")).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.Locator("mark.squiggle")).ToHaveTextAsync("\"Jo");
        await Assertions.Expect(Save).ToBeDisabledAsync();
        await Assertions.Expect(Dialog.GetByText("players ready to import.")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Import_Squiggle_IsARedWavyUnderline()
    {
        await OpenImportAsync();

        await Text.FillAsync("Ann");

        var style = await Dialog.Locator("mark.squiggle").EvaluateAsync<string[]>(
            "element => { const style = getComputedStyle(element); return [style.textDecorationLine, style.textDecorationStyle, style.textDecorationColor]; }");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(style[0], Does.Contain("underline"));
            Assert.That(style[1], Is.EqualTo("wavy"));
            Assert.That(style[2], Is.Not.EqualTo("rgba(0, 0, 0, 0)"));
        }
    }

    [Test]
    public async Task Import_ErrorFixed_EnablesSaveAgain()
    {
        await OpenImportAsync();
        await Text.FillAsync("Ann");
        await Assertions.Expect(Save).ToBeDisabledAsync();

        await Text.FillAsync("Ann, Smith");

        await Assertions.Expect(Save).ToBeEnabledAsync();
        await Assertions.Expect(Dialog.Locator("mark.squiggle")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Import_ManyErrors_ListsTheFirstFewThenHowManyMore()
    {
        await OpenImportAsync();

        await Text.FillAsync("a\nb\nc\nd\ne\nf\ng");

        await Assertions.Expect(Dialog.GetByText("Line 1: enter a first name and a last name, separated by a comma.")).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.GetByText("Line 5:")).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.GetByText("Line 6:")).ToHaveCountAsync(0);
        await Assertions.Expect(Dialog.GetByText("…and 2 more.")).ToBeVisibleAsync();
    }

    [TestCase("Ann, Smith, 5", "Line 1: the rank must be 1 to 3 (or left out).")]
    [TestCase("Ann, \"Smith\"x", "Line 1: there is text after a closing quote.")]
    [TestCase("Ann, Sm\"ith", "Line 1: quotes must go around the whole value.")]
    [TestCase("Ann, Smith, 1, 2", "Line 1: too many values: only first name, last name and rank.")]
    [TestCase(", Smith", "Line 1: the first name is missing.")]
    public async Task Import_EachKindOfMistake_ShowsItsMessage(string text, string message)
    {
        await OpenImportAsync();

        await Text.FillAsync(text);

        await Assertions.Expect(Dialog.GetByText(message)).ToBeVisibleAsync();
        await Assertions.Expect(Dialog.Locator("mark.squiggle")).Not.ToHaveCountAsync(0);
    }

    [Test]
    public async Task Import_NameOverFiftyCharacters_ShowsTheLengthMessage()
    {
        await OpenImportAsync();

        await Text.FillAsync($"Ann, {new string('x', 51)}");

        await Assertions.Expect(Dialog.GetByText("Line 1: names must be 50 characters or fewer.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Cancel_Clicked_ClosesTheDialogAndAddsNobody()
    {
        await OpenImportAsync();
        await Text.FillAsync("Ann, Smith");

        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();

        await Assertions.Expect(Dialog).ToHaveCountAsync(0);
        await Assertions.Expect(Page.GetByText("No players yet. Add your first player.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Import_Reopened_StartsEmpty()
    {
        await OpenImportAsync();
        await Text.FillAsync("Ann, Smith");
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Import", Exact = true }).ClickAsync();

        await Assertions.Expect(Text).ToHaveValueAsync(string.Empty);
    }
}
