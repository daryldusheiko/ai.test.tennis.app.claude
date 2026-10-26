namespace SocialTennis.Players;

/// <summary>Searching and sorting the player list. Plain functions so they are testable without a browser.</summary>
public static class PlayerList
{
    public static IReadOnlyList<(PlayerSortOrder Order, string Label)> SortOptions { get; } =
    [
        (PlayerSortOrder.FirstNameAscending, "First name (A–Z)"),
        (PlayerSortOrder.FirstNameDescending, "First name (Z–A)"),
        (PlayerSortOrder.LastNameAscending, "Last name (A–Z)"),
        (PlayerSortOrder.LastNameDescending, "Last name (Z–A)"),
    ];

    /// <summary>Keeps players where every word typed appears in the first or last name, ignoring case.</summary>
    public static IEnumerable<Player> Search(IEnumerable<Player> players, string? search)
    {
        ArgumentNullException.ThrowIfNull(players);

        var words = (search ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return players.Where(player => words.All(word =>
            player.FirstName.Contains(word, StringComparison.OrdinalIgnoreCase)
            || player.LastName.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Sorts by the chosen name, breaking ties on the other name.</summary>
    public static IReadOnlyList<Player> Sort(IEnumerable<Player> players, PlayerSortOrder order)
    {
        ArgumentNullException.ThrowIfNull(players);

        var comparer = StringComparer.OrdinalIgnoreCase;
        var ordered = order switch
        {
            PlayerSortOrder.FirstNameAscending => players.OrderBy(p => p.FirstName, comparer).ThenBy(p => p.LastName, comparer),
            PlayerSortOrder.FirstNameDescending => players.OrderByDescending(p => p.FirstName, comparer).ThenByDescending(p => p.LastName, comparer),
            PlayerSortOrder.LastNameAscending => players.OrderBy(p => p.LastName, comparer).ThenBy(p => p.FirstName, comparer),
            PlayerSortOrder.LastNameDescending => players.OrderByDescending(p => p.LastName, comparer).ThenByDescending(p => p.FirstName, comparer),
            _ => throw new ArgumentOutOfRangeException(nameof(order), order, "Unknown sort order."),
        };
        return [.. ordered];
    }

    /// <summary>Players to suggest while typing: those matching the search who are not already chosen, by first name, up to a limit.</summary>
    public static IReadOnlyList<Player> Suggest(IEnumerable<Player> players, string? search, IReadOnlySet<Guid> excluded, int limit)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(excluded);

        return string.IsNullOrWhiteSpace(search)
            ? []
            : [.. Sort(Search(players.Where(player => !excluded.Contains(player.Id)), search), PlayerSortOrder.FirstNameAscending).Take(limit)];
    }
}
