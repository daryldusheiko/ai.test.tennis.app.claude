using Microsoft.JSInterop;

namespace SocialTennis.Storage;

/// <summary>The backup file in the browser (<c>js/backup.js</c>). Only strings cross the interop boundary, which keeps it safe when trimmed.</summary>
public sealed class BackupFiles(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    /// <summary>True where the browser can create and open folders; false on iPhone Safari.</summary>
    public async Task<bool> SupportsFoldersAsync() => await (await ModuleAsync()).InvokeAsync<bool>("supportsFolders");

    public async Task<BackupSaveOutcome> SaveAsync(string fileName, string text)
    {
        var outcome = await (await ModuleAsync()).InvokeAsync<string>("save", fileName, text);
        return outcome switch
        {
            "folder" => BackupSaveOutcome.Folder,
            "shared" => BackupSaveOutcome.Shared,
            "downloaded" => BackupSaveOutcome.Downloaded,
            _ => BackupSaveOutcome.Cancelled,
        };
    }

    public async Task<BackupPick> PickAsync(int maxLength)
    {
        var module = await ModuleAsync();
        var outcome = await module.InvokeAsync<string>("pick", maxLength);
        return outcome switch
        {
            "ok" => new BackupPick(BackupPickOutcome.Ok, await module.InvokeAsync<string>("takeText")),
            "tooLarge" => new BackupPick(BackupPickOutcome.TooLarge, string.Empty),
            _ => new BackupPick(BackupPickOutcome.Cancelled, string.Empty),
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page is closing; there is nothing left to tidy up.
            }
        }
    }

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/backup.js");
}
