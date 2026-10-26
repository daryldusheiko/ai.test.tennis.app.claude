namespace SocialTennis.Players;

public sealed record BackupReadResult(BackupReadStatus Status, IReadOnlyList<Player> Players);
