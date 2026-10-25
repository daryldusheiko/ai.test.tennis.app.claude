using System.Text.Json;
using SocialTennis.Storage;

namespace SocialTennis.Scheduling;

/// <summary>Stores the one schedule on the device.</summary>
public sealed class ScheduleService(IKeyValueStorage storage)
{
    internal const string ScheduleKey = "socialTennis.schedule";

    public async Task<Schedule?> GetAsync()
    {
        var json = await storage.GetAsync(ScheduleKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var stored = JsonSerializer.Deserialize(json, ScheduleJsonContext.Default.Schedule);
            return stored is null ? null : Normalise(stored);
        }
        catch (JsonException)
        {
            // Unreadable data must never stop the app opening.
            return null;
        }
    }

    public Task SaveAsync(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return storage.SetAsync(ScheduleKey, JsonSerializer.Serialize(schedule, ScheduleJsonContext.Default.Schedule));
    }

    public Task DeleteAsync() => storage.SetAsync(ScheduleKey, string.Empty);

    // Fields missing from data saved by another version load as empty rather than null.
    private static Schedule Normalise(Schedule schedule) => schedule with
    {
        Courts = schedule.Courts ?? [],
        Players = schedule.Players ?? [],
        Rounds = [.. (schedule.Rounds ?? []).Select(round => round with
        {
            Games = [.. (round.Games ?? []).Select(game => game with { Team1 = game.Team1 ?? [], Team2 = game.Team2 ?? [] })],
            SittingOut = round.SittingOut ?? [],
        })],
    };
}
