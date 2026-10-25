namespace SocialTennis.Scheduling;

/// <summary>
/// Builds a session's rounds (see "Schedule rules" in spec.md). It is a seeded randomised search: each attempt builds rounds
/// one at a time, with backtracking inside a round, and attempts are repeated until every rule is met. The rules are tried
/// in order of importance: opponents at most twice, then at most three times, then (singles only) allowing a rank 1 to play a rank 3.
/// </summary>
public static class ScheduleEngine
{
    public const int MaxRounds = 6;

    public const int MinPlayers = 4;

    private const int NodesPerRound = 4000;

    private static readonly (int OpponentLimit, bool AllowRankClash, int Attempts)[] Phases = [(2, false, 60), (3, false, 120), (3, true, 60)];

    private enum SpecialKind
    {
        None,
        SitOut,
        ThreePlayer,
        Singles,
    }

    /// <summary>The most rounds that can be played without anyone partnering the same player twice.</summary>
    public static int RoundsPossible(int playerCount, int courtCount)
    {
        var shape = Shape.For(playerCount, courtCount);
        var rounds = MaxRounds;
        while (rounds > 1 && rounds - (rounds * shape.NoPartnerPlayers / playerCount) > playerCount - 1)
        {
            rounds--;
        }

        return rounds;
    }

    public static IReadOnlyList<Round> Generate(IReadOnlyList<SchedulePlayer> players, int courtCount, int seed)
    {
        ArgumentNullException.ThrowIfNull(players);
        if (players.Count < MinPlayers)
        {
            throw new ArgumentException($"A schedule needs at least {MinPlayers} players but got {players.Count}.", nameof(players));
        }

        if (courtCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(courtCount), courtCount, "At least one court is needed.");
        }

        var shape = Shape.For(players.Count, courtCount);
        var target = RoundsPossible(players.Count, courtCount);
        var ranks = players.Select(player => player.Rank).ToArray();
        var canClash = shape.Kind == SpecialKind.Singles && ranks.Contains(1) && ranks.Contains(3);
        var random = new Random(seed);

        Built? best = null;
        foreach (var (opponentLimit, allowRankClash, attempts) in Phases)
        {
            if (allowRankClash && !canClash)
            {
                continue;
            }

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var built = new Builder(shape, courtCount, opponentLimit, allowRankClash, ranks, random).Build(target);
                if (best is null || built.Rounds.Count > best.Rounds.Count
                    || (built.Rounds.Count == best.Rounds.Count && built.RankClashes < best.RankClashes))
                {
                    best = built;
                }

                if (best.Rounds.Count == target && best.RankClashes == 0)
                {
                    return ToRounds(best, players);
                }
            }

            if (best?.Rounds.Count == target && !allowRankClash)
            {
                return ToRounds(best, players);
            }
        }

        return ToRounds(best ?? throw new InvalidOperationException("No schedule could be built."), players);
    }

    private static List<Round> ToRounds(Built built, IReadOnlyList<SchedulePlayer> players)
    {
        return
        [
            .. built.Rounds.Select(round => new Round(
                [.. round.Games.Select(game => new Game(Ids(game.Team1, players), Ids(game.Team2, players), false))],
                Ids(round.SittingOut, players))),
        ];
    }

    private static Guid[] Ids(int[] indexes, IReadOnlyList<SchedulePlayer> players) => [.. indexes.Select(index => players[index].Id)];

    private sealed record Shape(int Doubles, SpecialKind Kind, int SpecialPlayers)
    {
        /// <summary>Players per round with no partner: sitting out, singles, or the lone player of a three-player game.</summary>
        public int NoPartnerPlayers => Kind switch
        {
            SpecialKind.None => 0,
            SpecialKind.ThreePlayer => 1,
            SpecialKind.SitOut or SpecialKind.Singles => SpecialPlayers,
            _ => throw new InvalidOperationException($"Unknown kind {Kind}."),
        };

        public static Shape For(int players, int courts)
        {
            var doubles = Math.Min(courts, players / 4);
            var left = players - (4 * doubles);
            var courtFree = doubles < courts;
            return left switch
            {
                0 => new Shape(doubles, SpecialKind.None, 0),
                3 when courtFree => new Shape(doubles, SpecialKind.ThreePlayer, 3),
                2 when courtFree => new Shape(doubles, SpecialKind.Singles, 2),
                _ => new Shape(doubles, SpecialKind.SitOut, left),
            };
        }
    }

    private sealed record BuiltGame(int[] Team1, int[] Team2);

    private sealed record BuiltRound(List<BuiltGame> Games, int[] SittingOut);

    private sealed record Built(List<BuiltRound> Rounds, int RankClashes);

    private sealed class Builder
    {
        private readonly Shape _shape;
        private readonly int _opponentLimit;
        private readonly bool _allowRankClash;
        private readonly int?[] _ranks;
        private readonly Random _random;
        private readonly int _count;
        private readonly bool[,] _partners;
        private readonly int[,] _opponents;
        private readonly int[] _specialCount;
        private readonly int[] _lonerCount;
        private int _nodes;

        public Builder(Shape shape, int courtCount, int opponentLimit, bool allowRankClash, int?[] ranks, Random random)
        {
            _shape = shape;
            _opponentLimit = opponentLimit;
            _allowRankClash = allowRankClash;
            _ranks = ranks;
            _random = random;
            _count = ranks.Length;
            _partners = new bool[_count, _count];
            _opponents = new int[_count, _count];
            _specialCount = new int[_count];
            _lonerCount = new int[_count];
            _ = courtCount;
        }

        public Built Build(int target)
        {
            var rounds = new List<BuiltRound>();
            var clashes = 0;
            while (rounds.Count < target && TryBuildRound(out var round, out var clash))
            {
                rounds.Add(round);
                clashes += clash;
            }

            return new Built(rounds, clashes);
        }

        private bool TryBuildRound(out BuiltRound round, out int clash)
        {
            round = new BuiltRound([], []);
            clash = 0;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                if (!PickSpecials(out var specials, out var special, out clash))
                {
                    continue;
                }

                var specialSet = specials.ToHashSet();
                var playing = Shuffled(Enumerable.Range(0, _count).Where(index => !specialSet.Contains(index)));
                var games = new List<BuiltGame>();
                _nodes = 0;
                if (!SolveDoubles(playing, games))
                {
                    continue;
                }

                if (special is not null)
                {
                    games.Add(special);
                }

                round = new BuiltRound(games, _shape.Kind == SpecialKind.SitOut ? specials : []);
                Commit(round, specials);
                return true;
            }

            return false;
        }

        /// <summary>Picks the players with the fewest turns at sitting out / three-player games / singles, so shares stay even.</summary>
        private bool PickSpecials(out int[] specials, out BuiltGame? special, out int clash)
        {
            specials = [];
            special = null;
            clash = 0;
            if (_shape.Kind == SpecialKind.None)
            {
                return true;
            }

            BuiltGame? fallback = null;
            int[] fallbackSpecials = [];
            for (var attempt = 0; attempt < 30; attempt++)
            {
                var chosen = Shuffled(Enumerable.Range(0, _count)).OrderBy(index => _specialCount[index]).Take(_shape.SpecialPlayers).ToArray();
                switch (_shape.Kind)
                {
                    case SpecialKind.None:
                        return true;
                    case SpecialKind.SitOut:
                        specials = chosen;
                        return true;
                    case SpecialKind.Singles:
                        {
                            var (a, b) = (chosen[0], chosen[1]);
                            if (_opponents[a, b] >= _opponentLimit)
                            {
                                continue;
                            }

                            var game = new BuiltGame([a], [b]);
                            if (!IsRankClash(a, b))
                            {
                                (specials, special, clash) = (chosen, game, 0);
                                return true;
                            }

                            if (_allowRankClash && fallback is null)
                            {
                                (fallback, fallbackSpecials) = (game, chosen);
                            }

                            break;
                        }

                    case SpecialKind.ThreePlayer:
                        {
                            var loners = chosen.Where(index => IsValidThreePlayer(index, chosen))
                                .OrderBy(index => _lonerCount[index]).ThenBy(_ => _random.Next()).ToArray();
                            if (loners.Length > 0)
                            {
                                special = new BuiltGame([loners[0]], [.. chosen.Where(index => index != loners[0])]);
                                specials = chosen;
                                return true;
                            }

                            break;
                        }

                    default:
                        throw new InvalidOperationException($"Unknown kind {_shape.Kind}.");
                }
            }

            if (fallback is not null)
            {
                (specials, special, clash) = (fallbackSpecials, fallback, 1);
                return true;
            }

            return false;
        }

        private bool IsValidThreePlayer(int loner, int[] chosen)
        {
            var pair = chosen.Where(index => index != loner).ToArray();
            return !_partners[pair[0], pair[1]]
                && _opponents[loner, pair[0]] < _opponentLimit
                && _opponents[loner, pair[1]] < _opponentLimit;
        }

        private bool IsRankClash(int a, int b) => (_ranks[a], _ranks[b]) is (1, 3) or (3, 1);

        private bool SolveDoubles(List<int> remaining, List<BuiltGame> games)
        {
            if (remaining.Count == 0)
            {
                return true;
            }

            if (++_nodes > NodesPerRound)
            {
                return false;
            }

            var p = remaining[0];
            foreach (var q in Candidates(remaining.Skip(1), _ => 0).Where(q => !_partners[p, q]))
            {
                var rest = remaining.Where(index => index != p && index != q).ToList();
                foreach (var r in Candidates(rest, index => _opponents[p, index] + _opponents[q, index]))
                {
                    if (_opponents[p, r] >= _opponentLimit || _opponents[q, r] >= _opponentLimit)
                    {
                        continue;
                    }

                    foreach (var s in Candidates(rest.Where(index => index != r), index => _opponents[p, index] + _opponents[q, index]))
                    {
                        if (_partners[r, s] || _opponents[p, s] >= _opponentLimit || _opponents[q, s] >= _opponentLimit)
                        {
                            continue;
                        }

                        games.Add(new BuiltGame([p, q], [r, s]));
                        Mark(p, q, r, s);
                        if (SolveDoubles([.. rest.Where(index => index != r && index != s)], games))
                        {
                            return true;
                        }

                        Unmark(p, q, r, s);
                        games.RemoveAt(games.Count - 1);
                        if (_nodes > NodesPerRound)
                        {
                            return false;
                        }
                    }
                }
            }

            return false;
        }

        // Lowest cost first, so players who have met least are matched first; the random part varies the result by seed.
        private IEnumerable<int> Candidates(IEnumerable<int> options, Func<int, int> cost) =>
            options.Select(index => (Index: index, Key: cost(index) + _random.NextDouble())).OrderBy(item => item.Key).Select(item => item.Index);

        private List<int> Shuffled(IEnumerable<int> items)
        {
            var list = items.ToList();
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }

            return list;
        }

        private void Mark(int p, int q, int r, int s) => Apply(p, q, r, s, 1);

        private void Unmark(int p, int q, int r, int s) => Apply(p, q, r, s, -1);

        private void Apply(int p, int q, int r, int s, int delta)
        {
            _partners[p, q] = _partners[q, p] = delta > 0;
            _partners[r, s] = _partners[s, r] = delta > 0;
            foreach (var a in new[] { p, q })
            {
                foreach (var b in new[] { r, s })
                {
                    _opponents[a, b] += delta;
                    _opponents[b, a] += delta;
                }
            }
        }

        private void Commit(BuiltRound round, int[] specials)
        {
            // Doubles games were recorded while solving; only the special game is recorded here.
            foreach (var index in specials)
            {
                _specialCount[index]++;
            }

            if (_shape.Kind == SpecialKind.Singles)
            {
                var game = round.Games[^1];
                AddOpponents(game.Team1, game.Team2);
            }
            else if (_shape.Kind == SpecialKind.ThreePlayer)
            {
                var game = round.Games[^1];
                _lonerCount[game.Team1[0]]++;
                _partners[game.Team2[0], game.Team2[1]] = _partners[game.Team2[1], game.Team2[0]] = true;
                AddOpponents(game.Team1, game.Team2);
            }
        }

        private void AddOpponents(int[] side1, int[] side2)
        {
            foreach (var a in side1)
            {
                foreach (var b in side2)
                {
                    _opponents[a, b]++;
                    _opponents[b, a]++;
                }
            }
        }
    }
}
