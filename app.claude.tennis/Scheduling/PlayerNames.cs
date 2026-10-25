namespace SocialTennis.Scheduling;

/// <summary>Names as shown on schedule cards: first name and last-name initial, or the full name if two would look the same.</summary>
public sealed class PlayerNames
{
    private readonly Dictionary<Guid, string> _names;

    public PlayerNames(IEnumerable<SchedulePlayer> players)
    {
        ArgumentNullException.ThrowIfNull(players);

        var list = players.ToList();
        var clashing = list
            .GroupBy(Short, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(player => player.Id))
            .ToHashSet();
        _names = list.ToDictionary(player => player.Id, player => clashing.Contains(player.Id) ? player.FullName : Short(player));
        Players = list.ToDictionary(player => player.Id);
    }

    public IReadOnlyDictionary<Guid, SchedulePlayer> Players { get; }

    public string NameOf(Guid id) => _names.GetValueOrDefault(id, "?");

    private static string Short(SchedulePlayer player) =>
        string.IsNullOrWhiteSpace(player.LastName) ? player.FirstName : $"{player.FirstName} {char.ToUpperInvariant(player.LastName[0])}.";
}
