namespace SocialTennis.Navigation;

/// <summary>
/// Remembers how the app was opened. When it opens on the Play address with nothing in play, the Play page sends the user to Players.
/// This happens once: after that, tapping Play stays on Play.
/// </summary>
public sealed class AppStart
{
    private bool _begun;

    private bool _playCheckPending;

    /// <summary>Called by the layout with the address the app opened on. Only the first call counts.</summary>
    public void Begin(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        if (_begun)
        {
            return;
        }

        _begun = true;
        _playCheckPending = relativePath.Trim('/').Length == 0;
    }

    /// <summary>True once, if the app opened on the Play address.</summary>
    public bool TakePlayCheck()
    {
        var pending = _playCheckPending;
        _playCheckPending = false;
        return pending;
    }
}
