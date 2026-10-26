using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace SocialTennis.BrowserTests;

public class PlayersBackupTests : AppTestBase
{
    private const string FolderExplanation = "Backups are kept in a social.tennis.app folder. The first time, choose Documents and the folder is created there.";

    private const string PhoneExplanation = "Keep backups in a social.tennis.app folder in Documents: choose Save to Files, open Documents, and create the folder the first time. To restore, open that folder (or wherever you saved the file).";

    private ILocator SaveBackup => Page.GetByRole(AriaRole.Button, new() { Name = "Save backup", Exact = true });

    private ILocator Restore => Page.GetByRole(AriaRole.Button, new() { Name = "Restore from backup", Exact = true });

    private ILocator Names => Page.Locator(".player-name");

    private static string BackupJson(params (string First, string Last)[] players) => JsonSerializer.Serialize(new
    {
        app = "social-tennis",
        version = 1,
        players = players.Select(player => new { id = Guid.NewGuid(), firstName = player.First, lastName = player.Last, rank = (int?)null, comment = string.Empty }),
    });

    private async Task OpenPlayersAsync(params (string First, string Last)[] seeded)
    {
        await Page.GotoAsync(RootUrl("players"));
        await WaitForAppAsync();
        if (seeded.Length > 0)
        {
            var players = seeded.Select(player => new { Id = Guid.NewGuid(), FirstName = player.First, LastName = player.Last, Rank = (int?)null, Comment = string.Empty });
            await Page.EvaluateAsync("json => localStorage.setItem('socialTennis.players', json)", JsonSerializer.Serialize(players));
            await Page.ReloadAsync();
        }

        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Backup" })).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator(".backup-section p").Nth(1)).Not.ToBeEmptyAsync();
    }

    /// <summary>Makes the page behave like iPhone Safari without a share sheet, so saving downloads the file.</summary>
    private async Task NoShareSheetAsync() =>
        await Page.AddInitScriptAsync("Object.defineProperty(navigator, 'share', { value: undefined }); Object.defineProperty(navigator, 'canShare', { value: undefined });");

    /// <summary>A browser that can open folders (Chrome or Edge on a computer), with a folder that records what is written to it.</summary>
    private async Task FolderBrowserAsync(string pickedName = "Documents") =>
        await Page.AddInitScriptAsync($$"""
            window.__files = {};
            window.__pickerCalls = [];
            window.__created = [];
            const folder = name => ({
                name,
                kind: 'directory',
                getFileHandle: async fileName => ({
                    createWritable: async () => ({ write: async text => { window.__files[fileName] = text; }, close: async () => {} }),
                    getFile: async () => new File([window.__files[fileName] ?? ''], fileName),
                }),
            });
            window.showDirectoryPicker = async options => {
                window.__pickerCalls.push(options);
                const picked = folder('{{pickedName}}');
                picked.getDirectoryHandle = async (name, opts) => { window.__created.push([name, opts]); return folder(name); };
                return picked;
            };
            window.showOpenFilePicker = async options => {
                window.__pickerCalls.push(options);
                return [{ getFile: async () => new File([window.__restoreText ?? ''], 'backup.json') }];
            };
            """);

    [Test]
    public async Task Backup_Section_ExplainsTheBackupAndOffersBothButtons()
    {
        await OpenPlayersAsync();

        await Assertions.Expect(Page.GetByText("Players are stored on this phone. Save a backup now and then so you can restore them if the app is ever removed.")).ToBeVisibleAsync();
        await Assertions.Expect(SaveBackup).ToBeVisibleAsync();
        await Assertions.Expect(Restore).ToBeEnabledAsync();
    }

    [Test]
    public async Task Backup_BrowserWithoutFolders_ExplainsSaveToFiles()
    {
        await OpenPlayersAsync();

        await Assertions.Expect(Page.GetByText(PhoneExplanation)).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText(FolderExplanation)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Backup_BrowserWithFolders_ExplainsTheDocumentsFolder()
    {
        await FolderBrowserAsync();
        await OpenPlayersAsync();

        await Assertions.Expect(Page.GetByText(FolderExplanation)).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText(PhoneExplanation)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task SaveBackup_NoPlayers_IsDisabled()
    {
        await OpenPlayersAsync();

        await Assertions.Expect(SaveBackup).ToBeDisabledAsync();
    }

    [Test]
    public async Task SaveBackup_NoShareSheet_DownloadsADatedJsonFile()
    {
        await NoShareSheetAsync();
        await OpenPlayersAsync(("Ann", "Smith"), ("Bob", "Jones"));

        var download = await Page.RunAndWaitForDownloadAsync(() => SaveBackup.ClickAsync());

        await Assertions.Expect(Page.GetByText("Backup of 2 players saved.")).ToBeVisibleAsync();
        Assert.That(download.SuggestedFilename, Does.Match(@"^social-tennis-players-\d{4}-\d{2}-\d{2}\.json$"));
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(await download.PathAsync() ?? throw new InvalidOperationException("No download.")));
        Assert.That(json.RootElement.GetProperty("Players").GetArrayLength(), Is.EqualTo(2));
    }

    [Test]
    public async Task SaveBackup_ShareSheetAvailable_SharesTheFile()
    {
        await Page.AddInitScriptAsync("navigator.canShare = () => true; navigator.share = async data => { window.__shared = data.files.map(file => file.name); };");
        await OpenPlayersAsync(("Ann", "Smith"));

        await SaveBackup.ClickAsync();

        await Assertions.Expect(Page.GetByText("Backup of 1 players saved.")).ToBeVisibleAsync();
        var shared = await Page.EvaluateAsync<string[]>("window.__shared");
        Assert.That(shared, Has.Length.EqualTo(1));
        Assert.That(shared[0], Does.Match(@"^social-tennis-players-\d{4}-\d{2}-\d{2}\.json$"));
    }

    [Test]
    public async Task SaveBackup_ShareSheetDismissed_ShowsNoMessage()
    {
        await Page.AddInitScriptAsync("navigator.canShare = () => true; navigator.share = async () => { throw new DOMException('dismissed', 'AbortError'); };");
        await OpenPlayersAsync(("Ann", "Smith"));

        await SaveBackup.ClickAsync();

        await Assertions.Expect(SaveBackup).ToBeEnabledAsync();
        await Assertions.Expect(Page.GetByText("Backup of 1 players saved.")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task SaveBackup_FolderBrowser_CreatesTheFolderInDocumentsAndWritesTheFile()
    {
        await FolderBrowserAsync();
        await OpenPlayersAsync(("Ann", "Smith"), ("Bob", "Jones"));

        await SaveBackup.ClickAsync();

        await Assertions.Expect(Page.GetByText("Backup of 2 players saved in social.tennis.app.")).ToBeVisibleAsync();
        var created = await Page.EvaluateAsync<string>("window.__created[0][0]");
        var startIn = await Page.EvaluateAsync<string>("window.__pickerCalls[0].startIn");
        var files = await Page.EvaluateAsync<string[]>("Object.keys(window.__files)");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(created, Is.EqualTo("social.tennis.app"));
            Assert.That(startIn, Is.EqualTo("documents"));
            Assert.That(files, Has.Length.EqualTo(1));
            Assert.That(files[0], Does.Match(@"^social-tennis-players-\d{4}-\d{2}-\d{2}\.json$"));
        }
    }

    [Test]
    public async Task SaveBackup_FolderAlreadyChosen_IsUsedWithoutCreatingAnother()
    {
        await FolderBrowserAsync(pickedName: "social.tennis.app");
        await OpenPlayersAsync(("Ann", "Smith"));

        await SaveBackup.ClickAsync();

        await Assertions.Expect(Page.GetByText("Backup of 1 players saved in social.tennis.app.")).ToBeVisibleAsync();
        Assert.That(await Page.EvaluateAsync<int>("window.__created.length"), Is.Zero);
    }

    [Test]
    public async Task Restore_BackupFile_AddsThePlayers()
    {
        await OpenPlayersAsync();
        var file = new FilePayload { Name = "backup.json", MimeType = "application/json", Buffer = Encoding.UTF8.GetBytes(BackupJson(("Ann", "Smith"), ("Bob", "Jones"))) };

        var chooser = await Page.RunAndWaitForFileChooserAsync(() => Restore.ClickAsync());
        await chooser.SetFilesAsync(file);

        await Assertions.Expect(Page.GetByText("Restored 2 players.")).ToBeVisibleAsync();
        await Assertions.Expect(Names).ToHaveTextAsync(["Ann Smith", "Bob Jones"]);
    }

    [Test]
    public async Task Restore_PlayersAlreadyInTheList_AreSkipped()
    {
        await OpenPlayersAsync(("Ann", "Smith"));
        var file = new FilePayload { Name = "backup.json", MimeType = "application/json", Buffer = Encoding.UTF8.GetBytes(BackupJson(("ann", "SMITH"), ("Bob", "Jones"))) };

        var chooser = await Page.RunAndWaitForFileChooserAsync(() => Restore.ClickAsync());
        await chooser.SetFilesAsync(file);

        await Assertions.Expect(Page.GetByText("Restored 1 players (1 were already here).")).ToBeVisibleAsync();
        await Assertions.Expect(Names).ToHaveTextAsync(["Ann Smith", "Bob Jones"]);
    }

    [Test]
    public async Task Restore_FileThatIsNotABackup_IsRejected()
    {
        await OpenPlayersAsync();
        var file = new FilePayload { Name = "notes.json", MimeType = "application/json", Buffer = Encoding.UTF8.GetBytes(/* lang=json,strict */ "{\"hello\":\"world\"}") };

        var chooser = await Page.RunAndWaitForFileChooserAsync(() => Restore.ClickAsync());
        await chooser.SetFilesAsync(file);

        await Assertions.Expect(Page.GetByText("That file isn't a Social Tennis players backup.")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("No players yet. Add your first player.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Restore_FileThatIsTooLarge_IsRejected()
    {
        await OpenPlayersAsync();
        var file = new FilePayload { Name = "huge.json", MimeType = "application/json", Buffer = new byte[2_100_000] };

        var chooser = await Page.RunAndWaitForFileChooserAsync(() => Restore.ClickAsync());
        await chooser.SetFilesAsync(file);

        await Assertions.Expect(Page.GetByText("That file is too large to be a players backup.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Restore_FolderBrowser_OpensThePickerInTheBackupFolder()
    {
        await FolderBrowserAsync();
        await OpenPlayersAsync();
        await Page.EvaluateAsync("text => { window.__restoreText = text; }", BackupJson(("Cat", "Zed")));

        await Restore.ClickAsync();

        await Assertions.Expect(Page.GetByText("Restored 1 players.")).ToBeVisibleAsync();
        Assert.That(await Page.EvaluateAsync<string>("window.__pickerCalls[0].startIn"), Is.EqualTo("documents"));
        await Assertions.Expect(Names).ToHaveTextAsync(["Cat Zed"]);
    }

    [Test]
    public async Task Backup_SavedThenRestoredAfterTheListIsLost_BringsEveryoneBack()
    {
        await NoShareSheetAsync();
        await OpenPlayersAsync(("Ann", "Smith"), ("Bob", "Jones"));
        var download = await Page.RunAndWaitForDownloadAsync(() => SaveBackup.ClickAsync());
        var path = await download.PathAsync() ?? throw new InvalidOperationException("No download.");
        await Page.EvaluateAsync("localStorage.removeItem('socialTennis.players')");
        await Page.ReloadAsync();
        await Assertions.Expect(Page.GetByText("No players yet. Add your first player.")).ToBeVisibleAsync();

        var chooser = await Page.RunAndWaitForFileChooserAsync(() => Restore.ClickAsync());
        await chooser.SetFilesAsync(path);

        await Assertions.Expect(Page.GetByText("Restored 2 players.")).ToBeVisibleAsync();
        await Assertions.Expect(Names).ToHaveTextAsync(["Ann Smith", "Bob Jones"]);
    }

    [Test]
    public async Task Backup_Section_OnIPhoneSe_DoesNotScrollHorizontally()
    {
        await OpenPlayersAsync(("Ann", "Smith"));

        var overflow = await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth");

        Assert.That(overflow, Is.False);
    }
}
