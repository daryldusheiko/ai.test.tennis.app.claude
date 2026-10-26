namespace SocialTennis.Players;

/// <summary>The sentences shown after importing, saving a backup or restoring one.</summary>
public static class PlayerMessages
{
    public static string ReadyToImport(int count) => count == 1 ? "1 player ready to import." : $"{count} players ready to import.";

    public static string Imported(int added, int skipped) =>
        skipped == 0 ? $"Imported {added} players." : $"Imported {added} players ({skipped} already here).";

    public static string Restored(int added, int skipped) =>
        skipped == 0 ? $"Restored {added} players." : $"Restored {added} players ({skipped} were already here).";

    public static string BackupSaved(int count, bool inFolder) =>
        inFolder ? $"Backup of {count} players saved in social.tennis.app." : $"Backup of {count} players saved.";
}
