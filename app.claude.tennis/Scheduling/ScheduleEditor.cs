namespace SocialTennis.Scheduling;

/// <summary>
/// Edits to a created schedule: pin, swap, and Re-schedule (see "Reviewing and editing the schedule" in spec.md). Plain functions:
/// each returns the schedule to keep, which is the input unchanged when the edit isn't allowed.
/// </summary>
public static class ScheduleEditor
{
    private const int StrictOpponentLimit = 2;

    private const int RelaxedOpponentLimit = 3;

    public static PlayerOptions OptionsFor(Schedule schedule, PlayerSpot spot)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var round = schedule.Rounds[spot.Round];
        var number = spot.Round + 1;
        var place = Locate(round, spot.Player) ?? throw new ArgumentException($"Player {spot.Player} is not in round {number}.", nameof(spot));

        var gameComplete = !place.IsSittingOut && round.Games[place.Game].IsComplete;
        return (round.IsComplete, gameComplete, round.Pinned.Contains(spot.Player), place.IsSittingOut) switch
        {
            (true, _, _, _) => new PlayerOptions($"Round {number} is complete, so nothing in it can be changed.", false, false, false),
            (_, true, _, _) => new PlayerOptions("This game is complete, so it can't be changed.", false, false, false),
            (_, _, true, _) => new PlayerOptions("Pinned in this game: re-scheduling won't move them and they can't be swapped.", false, true, false, true),
            (_, _, _, true) => new PlayerOptions($"Sitting out in round {number}.", false, false, true, true),
            _ => new PlayerOptions($"Round {number}.", true, false, true, true),
        };
    }

    public static EditResult Pin(Schedule schedule, PlayerSpot spot)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var options = OptionsFor(schedule, spot);
        if (!options.CanPin)
        {
            return new EditResult(schedule, options.Message, false);
        }

        var round = schedule.Rounds[spot.Round];
        var name = new PlayerNames(schedule.Players).NameOf(spot.Player);
        return new EditResult(WithRound(schedule, spot.Round, round with { Pinned = [.. round.Pinned, spot.Player] }), Sentence($"Pinned {name}"), true);
    }

    public static EditResult Unpin(Schedule schedule, PlayerSpot spot)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var options = OptionsFor(schedule, spot);
        if (!options.CanUnpin)
        {
            return new EditResult(schedule, options.Message, false);
        }

        var round = schedule.Rounds[spot.Round];
        var name = new PlayerNames(schedule.Players).NameOf(spot.Player);
        return new EditResult(WithRound(schedule, spot.Round, round with { Pinned = [.. round.Pinned.Where(id => id != spot.Player)] }), Sentence($"Unpinned {name}"), true);
    }

    /// <summary>Swaps two players within a round. A player sitting out can be swapped into a game.</summary>
    public static EditResult Swap(Schedule schedule, PlayerSpot first, PlayerSpot second)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var names = new PlayerNames(schedule.Players);
        EditResult Refuse(string message)
        {
            return new EditResult(schedule, message, false);
        }

        if (second.Round != first.Round || second.Player == first.Player)
        {
            return Refuse($"Pick a player in round {first.Round + 1}.");
        }

        var round = schedule.Rounds[first.Round];
        var firstPlace = Locate(round, first.Player);
        var secondPlace = Locate(round, second.Player);
        if (firstPlace is not { } a || secondPlace is not { } b)
        {
            return Refuse($"Pick a player in round {first.Round + 1}.");
        }

        foreach (var id in new[] { first.Player, second.Player })
        {
            if (round.Pinned.Contains(id))
            {
                return Refuse($"{names.NameOf(id)} is pinned and can't be swapped.");
            }
        }

        if (round.IsComplete || IsInCompleteGame(round, a) || IsInCompleteGame(round, b))
        {
            return Refuse("That game is complete, so it can't be changed.");
        }

        if (a.IsSittingOut && b.IsSittingOut)
        {
            return Refuse($"{names.NameOf(first.Player)} and {names.NameOf(second.Player)} are both sitting out.");
        }

        if (a.Game == b.Game && a.Team == b.Team)
        {
            return Refuse($"{names.NameOf(first.Player)} and {names.NameOf(second.Player)} are already on the same side.");
        }

        var changed = AddChanged(SwapPlayers(round, first.Player, second.Player), first.Player, second.Player);
        var swapped = WithRound(schedule, first.Round, changed);
        var message = Sentence($"Swapped {names.NameOf(first.Player)} and {names.NameOf(second.Player)}");
        if (RepeatPairs(swapped).Count > RepeatPairs(schedule).Count)
        {
            message += " Partners who play together twice are shown in red.";
        }

        return new EditResult(swapped, message, true);
    }

    /// <summary>Partners who play together more than once and weren't accepted by the user, with the rounds they do.</summary>
    public static IReadOnlyList<RepeatPair> RepeatPairs(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return RepeatPairs(schedule.Rounds, [.. schedule.AcceptedPairs]);
    }

    /// <summary>Repeat partnerships that involve a completed game and a game that can still change: the user decides who moves.</summary>
    public static IReadOnlyList<PlayedTogether> PendingChoices(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var pending = new List<PlayedTogether>();
        foreach (var repeat in RepeatPairs(schedule))
        {
            var completed = repeat.Rounds.Where(index => IsPartnerGameComplete(schedule.Rounds[index], repeat.Pair)).ToList();
            if (completed.Count == 0)
            {
                continue;
            }

            pending.AddRange(repeat.Rounds.Except(completed).Select(later => new PlayedTogether(repeat.Pair, completed[0], later)));
        }

        return pending;
    }

    /// <summary>Resolves red partners with as few moves as possible, without breaking the fairness or opponent rules.</summary>
    public static EditResult Reschedule(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var context = new Context([.. schedule.AcceptedPairs], schedule.Players);
        var names = new PlayerNames(schedule.Players);
        var rounds = schedule.Rounds.ToList();
        var moved = new HashSet<PlayerSpot>();

        foreach (var limit in new[] { StrictOpponentLimit, RelaxedOpponentLimit })
        {
            while (TryImprove(rounds, context, limit, moved, AutomaticMovers(rounds, context)))
            {
            }
        }

        var result = schedule with { Rounds = rounds };
        var pending = PendingChoices(result);
        var stuck = RepeatPairs(rounds, context.Accepted)
            .FirstOrDefault(repeat => repeat.Rounds.Any(index => !IsPartnerGameComplete(rounds[index], repeat.Pair))
                && !pending.Any(choice => choice.Pair == repeat.Pair));

        var message = moved.Count switch
        {
            0 => string.Empty,
            1 => "Re-scheduled: 1 player moved (shown in blue).",
            _ => $"Re-scheduled: {moved.Count} players moved (shown in blue).",
        };
        if (stuck is not null)
        {
            var separate = $"Couldn't separate {names.NameOf(stuck.Pair.First)} & {names.NameOf(stuck.Pair.Second)}: pinned players are in the way.";
            message = message.Length == 0 ? separate : $"{message} {separate}";
        }
        else if (message.Length == 0)
        {
            message = pending.Count == 0 ? "Nothing needed changing." : "Some partners already played together in a completed round: choose who should move.";
        }

        return new EditResult(result, message, stuck is null);
    }

    /// <summary>Moves one of two players who already played together in a completed game out of the later game they share.</summary>
    public static EditResult MoveToResolve(Schedule schedule, PlayedTogether conflict, Guid mover)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(conflict);
        if (!conflict.Pair.Contains(mover))
        {
            throw new ArgumentException($"Player {mover} is not in the pair.", nameof(mover));
        }

        var names = new PlayerNames(schedule.Players);
        var name = names.NameOf(mover);
        var round = schedule.Rounds[conflict.LaterRound];
        if (round.Pinned.Contains(mover))
        {
            return new EditResult(schedule, $"{name} is pinned and can't be moved.", false);
        }

        var context = new Context([.. schedule.AcceptedPairs], schedule.Players);
        var rounds = schedule.Rounds.ToList();
        var moved = new HashSet<PlayerSpot>();
        var movers = new List<(int Round, Guid Player)> { (conflict.LaterRound, mover) };
        var done = TryImprove(rounds, context, StrictOpponentLimit, moved, movers)
            || TryImprove(rounds, context, RelaxedOpponentLimit, moved, movers);
        return done
            ? new EditResult(schedule with { Rounds = rounds }, $"Moved {name} in round {conflict.LaterRound + 1} (shown in blue).", true)
            : new EditResult(schedule, $"Couldn't move {name} without breaking the schedule rules.", false);
    }

    /// <summary>Accepts the repeat partnership: the pair is no longer shown in red and Re-schedule leaves them together.</summary>
    public static EditResult AcceptPair(Schedule schedule, PlayerPair pair)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(pair);
        var names = new PlayerNames(schedule.Players);
        var accepted = schedule.AcceptedPairs.Contains(pair) ? schedule.AcceptedPairs : [.. schedule.AcceptedPairs, pair];
        return new EditResult(schedule with { AcceptedPairs = accepted }, Sentence($"Keeping {names.NameOf(pair.First)} & {names.NameOf(pair.Second)} together"), true);
    }

    /// <summary>Done (and Play) clear the blue markers.</summary>
    public static Schedule ClearChanged(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return schedule with { Rounds = [.. schedule.Rounds.Select(round => round with { Changed = [] })] };
    }

    private static bool TryImprove(List<Round> rounds, Context context, int limit, HashSet<PlayerSpot> moved, IReadOnlyList<(int Round, Guid Player)> movers)
    {
        var current = Evaluate(rounds, context, limit);
        if (current.Conflicts == 0)
        {
            return false;
        }

        (int Round, Round Swapped, Guid X, Guid Y, int Undo, Score Score)? best = null;
        foreach (var (roundIndex, x) in movers)
        {
            var round = rounds[roundIndex];
            if (Locate(round, x) is not { } from || from.IsSittingOut || round.IsComplete || round.Games[from.Game].IsComplete || round.Pinned.Contains(x))
            {
                continue;
            }

            foreach (var y in Everyone(round))
            {
                if (y == x || round.Pinned.Contains(y) || Locate(round, y) is not { } to
                    || (to.Game == from.Game && to.Team == from.Team) || IsInCompleteGame(round, to))
                {
                    continue;
                }

                var swapped = SwapPlayers(round, x, y);
                var trial = rounds.ToList();
                trial[roundIndex] = swapped;
                var score = Evaluate(trial, context, limit);
                if (score.Conflicts >= current.Conflicts || score.Fairness > current.Fairness
                    || score.Excess > current.Excess || score.RankClashes > current.RankClashes)
                {
                    continue;
                }

                // Prefer fixing a repeat in a round the user hasn't touched.
                var undo = (round.Changed.Contains(x) ? 1 : 0) + (round.Changed.Contains(y) ? 1 : 0);
                if (best is not { } leader || IsBetter(score, undo, leader.Score, leader.Undo))
                {
                    best = (roundIndex, swapped, x, y, undo, score);
                }
            }
        }

        if (best is not { } chosen)
        {
            return false;
        }

        rounds[chosen.Round] = AddChanged(chosen.Swapped, chosen.X, chosen.Y);
        moved.Add(new PlayerSpot(chosen.Round, chosen.X));
        moved.Add(new PlayerSpot(chosen.Round, chosen.Y));
        return true;
    }

    // Short names end in an initial ("Ann T."), so a full stop is only added when there isn't one already.
    internal static string Sentence(string text) => text.EndsWith('.') ? text : text + ".";

    private static bool IsBetter(Score score, int undo, Score other, int otherUndo) =>
        score.Conflicts != other.Conflicts ? score.Conflicts < other.Conflicts
        : undo != otherUndo ? undo < otherUndo
        : score.Excess < other.Excess;

    // Only repeats that can be fixed without asking the user: pairs involving a completed game wait for their choice.
    private static List<(int Round, Guid Player)> AutomaticMovers(List<Round> rounds, Context context)
    {
        var movers = new List<(int Round, Guid Player)>();
        foreach (var repeat in RepeatPairs(rounds, context.Accepted))
        {
            if (repeat.Rounds.Any(index => IsPartnerGameComplete(rounds[index], repeat.Pair)))
            {
                continue;
            }

            foreach (var index in repeat.Rounds)
            {
                movers.Add((index, repeat.Pair.First));
                movers.Add((index, repeat.Pair.Second));
            }
        }

        return movers;
    }

    private static Score Evaluate(IReadOnlyList<Round> rounds, Context context, int limit)
    {
        var conflicts = RepeatPairs(rounds, context.Accepted).Sum(repeat => repeat.Rounds.Count - 1);

        var opponents = new Dictionary<PlayerPair, int>();
        var rankClashes = 0;
        var sittingOut = context.Players.ToDictionary(player => player.Id, _ => 0);
        var threePlayer = context.Players.ToDictionary(player => player.Id, _ => 0);
        var singles = context.Players.ToDictionary(player => player.Id, _ => 0);
        var ranks = context.Players.ToDictionary(player => player.Id, player => player.Rank);

        foreach (var round in rounds)
        {
            foreach (var id in round.SittingOut)
            {
                Bump(sittingOut, id);
            }

            foreach (var game in round.Games)
            {
                foreach (var a in game.Team1)
                {
                    foreach (var b in game.Team2)
                    {
                        var pair = PlayerPair.Of(a, b);
                        opponents[pair] = opponents.GetValueOrDefault(pair) + 1;
                    }
                }

                var role = (game.Team1.Count, game.Team2.Count) switch
                {
                    (1, 1) => singles,
                    (1, 2) => threePlayer,
                    _ => null,
                };
                if (role is not null)
                {
                    foreach (var id in game.Team1.Concat(game.Team2))
                    {
                        Bump(role, id);
                    }
                }

                if ((game.Team1.Count, game.Team2.Count) == (1, 1)
                    && (ranks.GetValueOrDefault(game.Team1[0]), ranks.GetValueOrDefault(game.Team2[0])) is (1, 3) or (3, 1))
                {
                    rankClashes++;
                }
            }
        }

        var excess = opponents.Values.Sum(count => Math.Max(0, count - limit));
        var fairness = new[] { sittingOut, threePlayer, singles }.Sum(counts => counts.Count == 0 ? 0 : Math.Max(0, counts.Values.Max() - counts.Values.Min() - 1));
        return new Score(conflicts, excess, fairness, rankClashes);
    }

    private static void Bump(Dictionary<Guid, int> counts, Guid id) => counts[id] = counts.GetValueOrDefault(id) + 1;

    private static List<RepeatPair> RepeatPairs(IReadOnlyList<Round> rounds, HashSet<PlayerPair> accepted)
    {
        var occurrences = new Dictionary<PlayerPair, List<int>>();
        for (var index = 0; index < rounds.Count; index++)
        {
            foreach (var pair in Partnerships(rounds[index]))
            {
                if (!occurrences.TryGetValue(pair, out var list))
                {
                    occurrences[pair] = list = [];
                }

                list.Add(index);
            }
        }

        return [.. occurrences.Where(entry => entry.Value.Count > 1 && !accepted.Contains(entry.Key)).Select(entry => new RepeatPair(entry.Key, entry.Value))];
    }

    private static IEnumerable<PlayerPair> Partnerships(Round round) =>
        round.Games.SelectMany(game => new[] { game.Team1, game.Team2 }).Where(team => team.Count == 2).Select(team => PlayerPair.Of(team[0], team[1]));

    private static bool IsPartnerGameComplete(Round round, PlayerPair pair) =>
        round.Games.Any(game => game.IsComplete && new[] { game.Team1, game.Team2 }.Any(team => team.Count == 2 && PlayerPair.Of(team[0], team[1]) == pair));

    private static bool IsInCompleteGame(Round round, Place place) => !place.IsSittingOut && round.Games[place.Game].IsComplete;

    private static IEnumerable<Guid> Everyone(Round round) => round.Games.SelectMany(game => game.Team1.Concat(game.Team2)).Concat(round.SittingOut);

    internal static Place? Locate(Round round, Guid id)
    {
        for (var game = 0; game < round.Games.Count; game++)
        {
            var team1 = round.Games[game].Team1.ToList().IndexOf(id);
            if (team1 >= 0)
            {
                return new Place(game, 1, team1);
            }

            var team2 = round.Games[game].Team2.ToList().IndexOf(id);
            if (team2 >= 0)
            {
                return new Place(game, 2, team2);
            }
        }

        var sitting = round.SittingOut.ToList().IndexOf(id);
        return sitting >= 0 ? new Place(-1, 0, sitting) : null;
    }

    internal static Round SwapPlayers(Round round, Guid a, Guid b)
    {
        Guid Replace(Guid id)
        {
            return id == a ? b : id == b ? a : id;
        }

        Guid[] Map(IReadOnlyList<Guid> ids)
        {
            return [.. ids.Select(Replace)];
        }

        return round with
        {
            Games = [.. round.Games.Select(game => game with { Team1 = Map(game.Team1), Team2 = Map(game.Team2) })],
            SittingOut = Map(round.SittingOut),
        };
    }

    private static Round AddChanged(Round round, params Guid[] ids) => round with { Changed = [.. round.Changed.Union(ids)] };

    private static Schedule WithRound(Schedule schedule, int index, Round round)
    {
        var rounds = schedule.Rounds.ToList();
        rounds[index] = round;
        return schedule with { Rounds = rounds };
    }

    internal readonly record struct Place(int Game, int Team, int Index)
    {
        public bool IsSittingOut => Game < 0;
    }

    private readonly record struct Score(int Conflicts, int Excess, int Fairness, int RankClashes);

    private sealed record Context(HashSet<PlayerPair> Accepted, IReadOnlyList<SchedulePlayer> Players);
}
