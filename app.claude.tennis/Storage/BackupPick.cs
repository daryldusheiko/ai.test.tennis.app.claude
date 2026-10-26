namespace SocialTennis.Storage;

/// <summary>The result of picking a backup file.</summary>
public sealed record BackupPick(BackupPickOutcome Outcome, string Text);
