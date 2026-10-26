namespace SocialTennis.Storage;

/// <summary>How picking a backup file to restore ended.</summary>
public enum BackupPickOutcome
{
    /// <summary>A file was read; its text is in <see cref="BackupPick.Text"/>.</summary>
    Ok,

    TooLarge,

    Cancelled,
}
