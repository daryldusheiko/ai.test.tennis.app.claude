using System.Globalization;
using System.Text.Json;

namespace SocialTennis.Players;

/// <summary>Creating and reading players backups (see "Backup and restore" in spec.md). Plain logic: the file itself is handled in the browser.</summary>
public static class PlayerBackup
{
    public const string AppMarker = "social-tennis";

    public const int FormatVersion = 1;

    /// <summary>The largest backup read, in characters; far more than any list of players.</summary>
    public const int MaxLength = 2_000_000;

    public static string FileName(DateOnly date) =>
        $"social-tennis-players-{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.json";

    public static string Create(IEnumerable<Player> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        return JsonSerializer.Serialize(new PlayerBackupFile(AppMarker, FormatVersion, [.. players]), PlayerBackupJsonContext.Default.PlayerBackupFile);
    }

    /// <summary>Reads a backup. Players with no usable name are left out; ranks above 3 become 3, like the stored list.</summary>
    public static BackupReadResult Read(string? text)
    {
        if (text is not null && text.Length > MaxLength)
        {
            return new BackupReadResult(BackupReadStatus.TooLarge, []);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new BackupReadResult(BackupReadStatus.NotABackup, []);
        }

        try
        {
            var file = JsonSerializer.Deserialize(text, PlayerBackupJsonContext.Default.PlayerBackupFile);
            if (file is not { App: AppMarker, Players: not null })
            {
                return new BackupReadResult(BackupReadStatus.NotABackup, []);
            }

            var players = file.Players
                .Where(player => player is not null && !string.IsNullOrWhiteSpace(player.FirstName))
                .Select(player => player with
                {
                    FirstName = player.FirstName.Trim(),
                    LastName = (player.LastName ?? string.Empty).Trim(),
                    Comment = player.Comment ?? string.Empty,
                    Id = player.Id == Guid.Empty ? Guid.NewGuid() : player.Id,
                    Rank = player.Rank switch
                    {
                        null or < Player.MinRank => null,
                        > Player.MaxRank => Player.MaxRank,
                        var rank => rank,
                    },
                })
                .ToList();
            return new BackupReadResult(BackupReadStatus.Ok, players);
        }
        catch (JsonException)
        {
            return new BackupReadResult(BackupReadStatus.NotABackup, []);
        }
    }
}
