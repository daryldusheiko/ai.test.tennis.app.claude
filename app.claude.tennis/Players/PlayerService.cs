using System.Text.Json;
using SocialTennis.Storage;

namespace SocialTennis.Players;

/// <summary>Stores the players and the chosen sort on the device.</summary>
public sealed class PlayerService(IKeyValueStorage storage)
{
    internal const string PlayersKey = "socialTennis.players";

    internal const string SortKey = "socialTennis.playerSort";

    public async Task<IReadOnlyList<Player>> GetAllAsync()
    {
        var json = await storage.GetAsync(PlayersKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var stored = JsonSerializer.Deserialize(json, PlayerJsonContext.Default.ListPlayer) ?? [];
            return [.. stored.Select(Normalise)];
        }
        catch (JsonException)
        {
            // Unreadable data must never stop the app opening.
            return [];
        }
    }

    public async Task<Player?> GetAsync(Guid id) => (await GetAllAsync()).FirstOrDefault(player => player.Id == id);

    /// <summary>Adds the player, or replaces the one with the same id. Names are trimmed.</summary>
    public async Task SaveAsync(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        var clean = player with { FirstName = player.FirstName.Trim(), LastName = player.LastName.Trim() };
        var players = (await GetAllAsync()).ToList();
        var index = players.FindIndex(existing => existing.Id == clean.Id);
        if (index < 0)
        {
            players.Add(clean);
        }
        else
        {
            players[index] = clean;
        }

        await WriteAsync(players);
    }

    /// <summary>Adds players to the list, skipping any already there, and says how many were added and skipped.</summary>
    public async Task<MergeResult> AddRangeAsync(IEnumerable<Player> incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        var merged = PlayerMerge.Merge(await GetAllAsync(), incoming);
        if (merged.Added > 0)
        {
            await WriteAsync([.. merged.Players]);
        }

        return merged;
    }

    public async Task DeleteAsync(Guid id)
    {
        var players = (await GetAllAsync()).Where(player => player.Id != id).ToList();
        await WriteAsync(players);
    }

    public async Task<PlayerSortOrder> GetSortOrderAsync()
    {
        var text = await storage.GetAsync(SortKey);
        return Enum.TryParse<PlayerSortOrder>(text, out var order) && Enum.IsDefined(order) ? order : PlayerSortOrder.FirstNameAscending;
    }

    public Task SetSortOrderAsync(PlayerSortOrder order) => storage.SetAsync(SortKey, order.ToString());

    // Ranks above 3 stored by an older version are treated as 3.
    private static Player Normalise(Player player) => player with
    {
        FirstName = player.FirstName ?? string.Empty,
        LastName = player.LastName ?? string.Empty,
        Comment = player.Comment ?? string.Empty,
        Rank = player.Rank switch
        {
            null or < Player.MinRank => null,
            > Player.MaxRank => Player.MaxRank,
            var rank => rank,
        },
    };

    private Task WriteAsync(List<Player> players) =>
        storage.SetAsync(PlayersKey, JsonSerializer.Serialize(players, PlayerJsonContext.Default.ListPlayer));
}
