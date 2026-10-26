namespace SocialTennis.Storage;

/// <summary>How a backup file was saved, which decides what the Players page says afterwards.</summary>
public enum BackupSaveOutcome
{
    /// <summary>Written to the social.tennis.app folder.</summary>
    Folder,

    /// <summary>Given to the share sheet (Save to Files, AirDrop, email).</summary>
    Shared,

    /// <summary>Downloaded, where there is no share sheet.</summary>
    Downloaded,

    /// <summary>The user dismissed the folder picker or the share sheet.</summary>
    Cancelled,
}
