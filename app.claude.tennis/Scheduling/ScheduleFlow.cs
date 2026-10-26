namespace SocialTennis.Scheduling;

/// <summary>The steps of creating a schedule. Plain functions: each returns the schedule to save.</summary>
public static class ScheduleFlow
{
    public const int SearchSuggestionLimit = 8;

    public static Schedule Start(Guid id) => new(id, ScheduleStep.Courts, [], [], [], 0);

    public static Schedule WithCourts(Schedule schedule, IReadOnlyList<int> courts)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(courts);
        return schedule with { Courts = [.. courts.Order()], Step = ScheduleStep.Players, Rounds = [] };
    }

    public static Schedule WithPlayers(Schedule schedule, IReadOnlyList<SchedulePlayer> players)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(players);
        return schedule with { Players = players };
    }

    public static Schedule BackToCourts(Schedule schedule, IReadOnlyList<SchedulePlayer> players) =>
        WithPlayers(schedule, players) with { Step = ScheduleStep.Courts };

    public static Schedule BackToPlayers(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return schedule with { Step = ScheduleStep.Players, Rounds = [] };
    }

    /// <summary>Builds the rounds. Names and ranks are taken from the current player list so late edits are used.</summary>
    public static Schedule Generate(Schedule schedule, IReadOnlyList<Players.Player> current, int seed)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(current);

        var players = schedule.Players
            .Select(chosen => current.FirstOrDefault(player => player.Id == chosen.Id) is { } latest
                ? new SchedulePlayer(latest.Id, latest.FirstName, latest.LastName, latest.Rank)
                : chosen)
            .ToList();
        var rounds = ScheduleEngine.Generate(players, schedule.Courts.Count, seed);
        return schedule with { Players = players, Rounds = rounds, Seed = seed, Step = ScheduleStep.Review };
    }

    public static Schedule Play(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return schedule with { Step = ScheduleStep.InPlay };
    }

    /// <summary>Moves a round to a new position. Completed rounds can't move, and nothing can move onto one.</summary>
    public static Schedule MoveRound(Schedule schedule, int from, int to)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return CanMove(schedule.Rounds, from, to, round => round.IsComplete)
            ? schedule with { Rounds = Moved(schedule.Rounds, from, to) }
            : schedule;
    }

    /// <summary>Moves a game within its round. Completed games can't move, and nothing can move onto one.</summary>
    public static Schedule MoveGame(Schedule schedule, int roundIndex, int from, int to)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (roundIndex < 0 || roundIndex >= schedule.Rounds.Count)
        {
            return schedule;
        }

        var round = schedule.Rounds[roundIndex];
        if (round.IsComplete || !CanMove(round.Games, from, to, game => game.IsComplete))
        {
            return schedule;
        }

        var rounds = schedule.Rounds.ToList();
        rounds[roundIndex] = round with { Games = Moved(round.Games, from, to) };
        return schedule with { Rounds = rounds };
    }

    private static bool CanMove<T>(IReadOnlyList<T> items, int from, int to, Func<T, bool> isComplete) =>
        from != to
        && from >= 0 && from < items.Count
        && to >= 0 && to < items.Count
        && !isComplete(items[from])
        && !isComplete(items[to]);

    private static List<T> Moved<T>(IReadOnlyList<T> items, int from, int to)
    {
        var list = items.ToList();
        var item = list[from];
        list.RemoveAt(from);
        list.Insert(to, item);
        return list;
    }
}
