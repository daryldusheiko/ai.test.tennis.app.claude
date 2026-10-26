namespace SocialTennis.Players;

/// <summary>The file format of a players backup. The marker tells a backup apart from any other JSON file.</summary>
public sealed record PlayerBackupFile(string App, int Version, List<Player> Players);
