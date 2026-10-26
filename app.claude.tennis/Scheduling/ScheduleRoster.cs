namespace SocialTennis.Scheduling;

/// <summary>
/// Adding and removing players once a schedule exists (see "Remove from schedule" and "Add a player" in spec.md). Rounds that are
/// complete never change; the others are built again for the new set of players, carrying on from the rounds already played.
/// </summary>
public static class ScheduleRoster
{
    /// <summary>The players still in the schedule: those in a round that isn't complete (or in any round when every round is complete).</summary>
    public static IReadOnlyList<SchedulePlayer> ActivePlayers(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var open = schedule.Rounds.Where(round => !round.IsComplete).ToList();
        var inRounds = (open.Count > 0 ? open : schedule.Rounds).SelectMany(Everyone).ToHashSet();
        return [.. schedule.Players.Where(player => inRounds.Contains(player.Id))];
    }

    /// <summary>Indexes of the rounds that can still change.</summary>
    public static IReadOnlyList<int> OpenRounds(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return [.. Enumerable.Range(0, schedule.Rounds.Count).Where(index => !schedule.Rounds[index].IsComplete)];
    }

    /// <summary>Round numbers in words: "round 3", "rounds 3–6" or "rounds 2, 4, 5". Takes zero-based indexes.</summary>
    public static string DescribeRounds(IEnumerable<int> indexes)
    {
        ArgumentNullException.ThrowIfNull(indexes);
        var numbers = indexes.Order().Select(index => index + 1).ToList();
        if (numbers.Count == 0)
        {
            return string.Empty;
        }

        if (numbers.Count == 1)
        {
            return $"round {numbers[0]}";
        }

        var contiguous = numbers[^1] - numbers[0] == numbers.Count - 1;
        return contiguous ? $"rounds {numbers[0]}–{numbers[^1]}" : $"rounds {string.Join(", ", numbers)}";
    }

    /// <summary>The message of the confirmation dialog for removing a player.</summary>
    public static string RemovalMessage(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var open = OpenRounds(schedule);
        if (open.Count == 0)
        {
            return "They are taken out of the schedule. Every round is complete, so the rounds don't change.";
        }

        var message = $"They are taken out of {DescribeRounds(open)} and the schedule is re-arranged.";
        return open.Count < schedule.Rounds.Count ? message + " Completed rounds keep them." : message;
    }

    public static EditResult Remove(Schedule schedule, Guid playerId, int seed)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var active = ActivePlayers(schedule);
        var leaving = active.FirstOrDefault(player => player.Id == playerId);
        if (leaving is null)
        {
            return new EditResult(schedule, "That player is not in the schedule.", false);
        }

        var name = new PlayerNames(schedule.Players).NameOf(playerId);
        if (active.Count - 1 < ScheduleEngine.MinPlayers)
        {
            return new EditResult(schedule, $"A schedule needs at least {ScheduleEngine.MinPlayers} players, so {name} can't be removed.", false);
        }

        var open = OpenRounds(schedule);
        if (open.Count == 0)
        {
            return new EditResult(schedule, ScheduleEditor.Sentence($"Removed {name}"), true);
        }

        var staying = active.Where(player => player.Id != playerId).ToList();
        var rebuilt = Rebuild(schedule, staying, open, seed, newcomer: null);
        var kept = rebuilt.Rounds.SelectMany(Everyone).ToHashSet();
        var players = schedule.Players.Where(player => player.Id != playerId || kept.Contains(playerId)).ToList();
        var result = Tidy(rebuilt with { Players = players });
        return new EditResult(result, WithPinWarning(ScheduleEditor.Sentence($"Removed {name} from {DescribeRounds(open)}"), result), true);
    }

    public static EditResult Add(Schedule schedule, SchedulePlayer player, int seed)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(player);
        if (ActivePlayers(schedule).Any(existing => existing.Id == player.Id))
        {
            return new EditResult(schedule, $"{player.FullName} is already in the schedule.", false);
        }

        var open = OpenRounds(schedule);
        if (open.Count == 0)
        {
            return new EditResult(schedule, $"Every round is complete, so there's nowhere to add {player.FullName}.", false);
        }

        var players = schedule.Players.Where(existing => existing.Id != player.Id).Append(player).ToList();
        var population = ActivePlayers(schedule).Append(player).ToList();
        var rebuilt = Rebuild(schedule with { Players = players }, population, open, seed, player.Id);
        var result = Tidy(rebuilt);
        var playing = open.Where(index => index < result.Rounds.Count && result.Rounds[index].Changed.Contains(player.Id)).ToList();
        var name = new PlayerNames(result.Players).NameOf(player.Id);
        var message = playing.Count == 0
            ? ScheduleEditor.Sentence($"Added {name}")
            : ScheduleEditor.Sentence($"Added {name} to {DescribeRounds(playing)} (shown in blue)");
        return new EditResult(result, WithPinWarning(message, result), true);
    }

    private static Schedule Rebuild(Schedule schedule, IReadOnlyList<SchedulePlayer> population, IReadOnlyList<int> open, int seed, Guid? newcomer)
    {
        var history = schedule.Rounds.Where(round => round.IsComplete).ToList();
        var built = ScheduleEngine.Continue(population, schedule.Courts.Count, open.Count, history, seed);
        var present = population.Select(player => player.Id).ToHashSet();
        var rounds = schedule.Rounds.ToList();
        for (var position = 0; position < open.Count; position++)
        {
            var old = schedule.Rounds[open[position]];
            if (position >= built.Count)
            {
                continue;
            }

            var next = RestorePins(old, built[position], present);
            if (newcomer is { } id && next.Games.Any(game => game.Team1.Contains(id) || game.Team2.Contains(id)))
            {
                next = next with { Changed = [id] };
            }

            rounds[open[position]] = next with { IsStarted = old.IsStarted };
        }

        // If the search could not build every round, the last open rounds are dropped rather than left with a player who left.
        var dropped = open.Skip(built.Count).ToHashSet();
        return schedule with { Rounds = [.. rounds.Where((_, index) => !dropped.Contains(index))] };
    }

    // Pinned players keep their place (game, side and position) where the new round has one; whoever is there swaps with them.
    private static Round RestorePins(Round old, Round next, HashSet<Guid> present)
    {
        var round = next;
        var pinned = old.Pinned.Where(present.Contains).ToList();
        foreach (var id in pinned)
        {
            if (ScheduleEditor.Locate(old, id) is not { IsSittingOut: false } wanted || wanted.Game >= round.Games.Count)
            {
                continue;
            }

            var side = wanted.Team == 1 ? round.Games[wanted.Game].Team1 : round.Games[wanted.Game].Team2;
            if (wanted.Index >= side.Count)
            {
                continue;
            }

            var occupant = side[wanted.Index];
            if (occupant != id && !pinned.Contains(occupant) && ScheduleEditor.Locate(round, id) is not null)
            {
                round = ScheduleEditor.SwapPlayers(round, id, occupant);
            }
        }

        return round with { Pinned = pinned };
    }

    // Repeats that can be fixed without breaking the rules are fixed now, so the user is not left with red names to chase.
    private static Schedule Tidy(Schedule schedule) =>
        ScheduleEditor.RepeatPairs(schedule).Count == 0 ? schedule : ScheduleEditor.Reschedule(schedule).Schedule;

    private static string WithPinWarning(string message, Schedule schedule) =>
        ScheduleEditor.RepeatPairs(schedule).Count > 0 && schedule.Rounds.Any(round => round.Pinned.Count > 0)
            ? message + " Some partners still play together twice (red): pinned players are in the way."
            : message;

    private static IEnumerable<Guid> Everyone(Round round) => round.Games.SelectMany(game => game.Team1.Concat(game.Team2)).Concat(round.SittingOut);
}
