using SocialTennis.Scheduling;

namespace SocialTennis.Tests.Scheduling;

/// <summary>Checks a schedule against the spec's rules. Written independently of the engine: it shares no code with it.</summary>
internal static class ScheduleChecker
{
    public static List<string> Check(IReadOnlyList<SchedulePlayer> players, int courts, IReadOnlyList<Round> rounds, int opponentLimit, bool rankRule)
    {
        var problems = new List<string>();
        var n = players.Count;
        var partnerCounts = new Dictionary<(Guid, Guid), int>();
        var opponentCounts = new Dictionary<(Guid, Guid), int>();
        var sat = players.ToDictionary(p => p.Id, _ => 0);
        var threePlayer = players.ToDictionary(p => p.Id, _ => 0);
        var singles = players.ToDictionary(p => p.Id, _ => 0);
        var rankOf = players.ToDictionary(p => p.Id, p => p.Rank);

        for (var r = 0; r < rounds.Count; r++)
        {
            var round = rounds[r];
            var seen = new List<Guid>();
            var doubles = 0;
            var three = 0;
            var single = 0;
            foreach (var game in round.Games)
            {
                seen.AddRange(game.Team1);
                seen.AddRange(game.Team2);
                var shape = (game.Team1.Count, game.Team2.Count);
                if (shape == (2, 2))
                {
                    doubles++;
                }
                else if (shape == (1, 2))
                {
                    three++;
                    foreach (var id in game.Team1.Concat(game.Team2))
                    {
                        threePlayer[id]++;
                    }
                }
                else if (shape == (1, 1))
                {
                    single++;
                    foreach (var id in game.Team1.Concat(game.Team2))
                    {
                        singles[id]++;
                    }

                    if (rankRule && (rankOf[game.Team1[0]], rankOf[game.Team2[0]]) is (1, 3) or (3, 1))
                    {
                        problems.Add($"round {r + 1}: rank 1 plays rank 3 in singles");
                    }
                }
                else
                {
                    problems.Add($"round {r + 1}: odd game shape {shape}");
                }

                foreach (var team in new[] { game.Team1, game.Team2 }.Where(t => t.Count == 2))
                {
                    var key = Key(team[0], team[1]);
                    partnerCounts[key] = partnerCounts.GetValueOrDefault(key) + 1;
                }

                foreach (var a in game.Team1)
                {
                    foreach (var b in game.Team2)
                    {
                        var key = Key(a, b);
                        opponentCounts[key] = opponentCounts.GetValueOrDefault(key) + 1;
                    }
                }
            }

            foreach (var id in round.SittingOut)
            {
                sat[id]++;
            }

            seen.AddRange(round.SittingOut);
            if (seen.Count != n || seen.Distinct().Count() != n || !players.All(p => seen.Contains(p.Id)))
            {
                problems.Add($"round {r + 1}: players are not each used exactly once");
            }

            var expectedDoubles = Math.Min(courts, n / 4);
            var left = n - (4 * expectedDoubles);
            var free = expectedDoubles < courts;
            var expectedThree = left == 3 && free ? 1 : 0;
            var expectedSingles = left == 2 && free ? 1 : 0;
            if (doubles != expectedDoubles || three != expectedThree || single != expectedSingles)
            {
                problems.Add($"round {r + 1}: games are {doubles}/{three}/{single} but expected {expectedDoubles}/{expectedThree}/{expectedSingles}");
            }

            if (round.Games.Count > courts)
            {
                problems.Add($"round {r + 1}: more games than courts");
            }
        }

        problems.AddRange(partnerCounts.Where(kv => kv.Value > 1).Select(kv => $"a pair partners {kv.Value} times"));
        problems.AddRange(opponentCounts.Where(kv => kv.Value > opponentLimit).Select(kv => $"a pair are opponents {kv.Value} times"));
        foreach (var (name, counts) in new[] { ("sit-outs", sat), ("three-player games", threePlayer), ("singles", singles) })
        {
            if (counts.Values.Max() - counts.Values.Min() > 1)
            {
                problems.Add($"{name} are not shared evenly ({counts.Values.Min()}..{counts.Values.Max()})");
            }
        }

        return problems;
    }

    public static int MaxOpponentCount(IReadOnlyList<Round> rounds) => rounds
        .SelectMany(round => round.Games)
        .SelectMany(game => game.Team1.SelectMany(a => game.Team2.Select(b => Key(a, b))))
        .GroupBy(key => key)
        .Max(group => group.Count());

    private static (Guid First, Guid Second) Key(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);
}
