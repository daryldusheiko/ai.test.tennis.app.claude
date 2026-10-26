namespace SocialTennis.Players;

/// <summary>Adds incoming players to the list, skipping any already there (same first and last name, ignoring case; or the same id).</summary>
public static class PlayerMerge
{
    public static MergeResult Merge(IReadOnlyList<Player> existing, IEnumerable<Player> incoming)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(incoming);

        var players = existing.ToList();
        var ids = existing.Select(player => player.Id).ToHashSet();
        var names = existing.Select(NameKey).ToHashSet();
        var added = 0;
        var skipped = 0;
        foreach (var player in incoming)
        {
            if (ids.Contains(player.Id) || !names.Add(NameKey(player)))
            {
                skipped++;
                continue;
            }

            ids.Add(player.Id);
            players.Add(player);
            added++;
        }

        return new MergeResult(players, added, skipped);
    }

    public static string NameKey(Player player) =>
        $"{player.FirstName.Trim().ToUpperInvariant()}|{player.LastName.Trim().ToUpperInvariant()}";
}
