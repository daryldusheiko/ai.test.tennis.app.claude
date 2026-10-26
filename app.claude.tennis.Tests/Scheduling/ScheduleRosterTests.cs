using SocialTennis.Scheduling;

namespace SocialTennis.Tests.Scheduling;

public class ScheduleRosterTests
{
    private static readonly string[] Names = ["Ann", "Bob", "Cat", "Dan", "Eve", "Fay", "Gus", "Hal"];

    private static readonly int[] Seeds = [1, 2, 3, 4, 5, 6, 7, 8];

    private List<SchedulePlayer> _players = [];

    [SetUp]
    public void CreatePlayers() =>
        _players = [.. Names.Select(name => new SchedulePlayer(Guid.NewGuid(), name, "Test", null))];

    private Guid Id(int index) => _players[index].Id;

    private Game DoublesGame(int a, int b, int c, int d, bool complete = false) => new([Id(a), Id(b)], [Id(c), Id(d)], complete);

    /// <summary>Eight players on two courts, three rounds, nobody partners twice. The first round can be complete.</summary>
    private Schedule Eight(bool firstRoundComplete = false) => new(
        Guid.NewGuid(),
        ScheduleStep.Review,
        [1, 2],
        _players,
        [
            new Round([DoublesGame(0, 1, 2, 3, firstRoundComplete), DoublesGame(4, 5, 6, 7, firstRoundComplete)], []),
            new Round([DoublesGame(0, 2, 4, 6), DoublesGame(1, 3, 5, 7)], []),
            new Round([DoublesGame(0, 4, 1, 5), DoublesGame(2, 6, 3, 7)], []),
        ],
        1);

    private static Schedule AllComplete(Schedule schedule) => schedule with
    {
        Rounds = [.. schedule.Rounds.Select(round => round with { Games = [.. round.Games.Select(game => game with { IsComplete = true })] })],
    };

    private static List<Guid> PlayersIn(Round round) =>
        [.. round.Games.SelectMany(game => game.Team1.Concat(game.Team2)), .. round.SittingOut];

    // Records hold arrays, which compare by reference, so rounds are compared by who is where.
    private static string Layout(Round round) =>
        string.Join('|', round.Games.Select(game => $"{string.Join(',', game.Team1)}v{string.Join(',', game.Team2)}").Append(string.Join(',', round.SittingOut)));

    private static int RepeatedPartnerships(IEnumerable<Round> rounds) =>
        rounds.SelectMany(round => round.Games.SelectMany(game => new[] { game.Team1, game.Team2 }).Where(team => team.Count == 2))
            .GroupBy(team => team[0].CompareTo(team[1]) < 0 ? (team[0], team[1]) : (team[1], team[0]))
            .Count(group => group.Count() > 1);

    [TestCase(new[] { 2 }, "round 3")]
    [TestCase(new[] { 2, 3, 4, 5 }, "rounds 3–6")]
    [TestCase(new[] { 1, 2 }, "rounds 2–3")]
    [TestCase(new[] { 1, 3, 4 }, "rounds 2, 4, 5")]
    [TestCase(new[] { 5, 0 }, "rounds 1, 6")]
    public void DescribeRounds_Indexes_UsesTheSpecWording(int[] indexes, string expected) =>
        Assert.That(ScheduleRoster.DescribeRounds(indexes), Is.EqualTo(expected));

    [Test]
    public void DescribeRounds_None_IsEmpty() =>
        Assert.That(ScheduleRoster.DescribeRounds([]), Is.Empty);

    [Test]
    public void RemovalMessage_NothingComplete_NamesEveryRound() =>
        Assert.That(ScheduleRoster.RemovalMessage(Eight()), Is.EqualTo("They are taken out of rounds 1–3 and the schedule is re-arranged."));

    [Test]
    public void RemovalMessage_SomeRoundsComplete_SaysCompletedRoundsKeepThem() =>
        Assert.That(ScheduleRoster.RemovalMessage(Eight(firstRoundComplete: true)),
            Is.EqualTo("They are taken out of rounds 2–3 and the schedule is re-arranged. Completed rounds keep them."));

    [Test]
    public void RemovalMessage_EveryRoundComplete_SaysTheRoundsDoNotChange() =>
        Assert.That(ScheduleRoster.RemovalMessage(AllComplete(Eight())),
            Is.EqualTo("They are taken out of the schedule. Every round is complete, so the rounds don't change."));

    [Test]
    public void ActivePlayers_OnlyPlayersInRoundsThatCanStillChange()
    {
        var schedule = Eight(firstRoundComplete: true);
        var removed = ScheduleRoster.Remove(schedule, Id(7), seed: 1).Schedule;

        var active = ScheduleRoster.ActivePlayers(removed);

        Assert.That(active.Select(player => player.Id), Is.EquivalentTo(_players.Take(7).Select(player => player.Id)));
    }

    [Test]
    public void ActivePlayers_EveryRoundComplete_IsEveryoneInTheRounds() =>
        Assert.That(ScheduleRoster.ActivePlayers(AllComplete(Eight())), Has.Count.EqualTo(8));

    [Test]
    public void Remove_PlayerInOpenRounds_TakesThemOutOfEveryOpenRound()
    {
        var result = ScheduleRoster.Remove(Eight(), Id(7), seed: 3);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Message, Is.EqualTo("Removed Hal T. from rounds 1–3."));
            Assert.That(result.Schedule.Rounds, Has.Count.EqualTo(3));
            Assert.That(result.Schedule.Rounds.SelectMany(PlayersIn), Does.Not.Contain(Id(7)));
            Assert.That(result.Schedule.Players.Select(player => player.Id), Does.Not.Contain(Id(7)));
        }
    }

    [Test]
    public void Remove_Seeded_EveryOpenRoundHasEveryoneExactlyOnceAndNoRepeatPartners()
    {
        foreach (var seed in Seeds)
        {
            var result = ScheduleRoster.Remove(Eight(), Id(3), seed);

            var expected = _players.Where(player => player.Id != Id(3)).Select(player => player.Id).ToList();
            foreach (var round in result.Schedule.Rounds)
            {
                Assert.That(PlayersIn(round), Is.EquivalentTo(expected), $"seed {seed}");
            }

            Assert.That(RepeatedPartnerships(result.Schedule.Rounds), Is.Zero, $"seed {seed}");
        }
    }

    [Test]
    public void Remove_SevenPlayersTwoCourts_PlaysOneDoublesAndOneThreePlayerGame()
    {
        var result = ScheduleRoster.Remove(Eight(), Id(0), seed: 2);

        foreach (var round in result.Schedule.Rounds)
        {
            Assert.That(round.Games.Select(game => (game.Team1.Count, game.Team2.Count)).Order(), Is.EqualTo([(1, 2), (2, 2)]));
        }
    }

    [Test]
    public void Remove_CompletedRound_IsLeftExactlyAsItWas()
    {
        var schedule = Eight(firstRoundComplete: true);

        var result = ScheduleRoster.Remove(schedule, Id(7), seed: 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Message, Is.EqualTo("Removed Hal T. from rounds 2–3."));
            Assert.That(result.Schedule.Rounds[0], Is.EqualTo(schedule.Rounds[0]));
            Assert.That(PlayersIn(result.Schedule.Rounds[0]), Does.Contain(Id(7)));
            Assert.That(result.Schedule.Players.Select(player => player.Id), Does.Contain(Id(7)));
        }
    }

    [Test]
    public void Remove_OpenRoundsContinueFromTheCompletedOne_NobodyPartnersAgain()
    {
        foreach (var seed in Seeds)
        {
            var result = ScheduleRoster.Remove(Eight(firstRoundComplete: true), Id(7), seed);

            Assert.That(RepeatedPartnerships(result.Schedule.Rounds), Is.Zero, $"seed {seed}");
        }
    }

    [Test]
    public void Remove_FourPlayersLeft_IsRefused()
    {
        var four = Eight() with
        {
            Players = [.. _players.Take(4)],
            Rounds = [new Round([DoublesGame(0, 1, 2, 3)], []), new Round([DoublesGame(0, 2, 1, 3)], [])],
        };

        var result = ScheduleRoster.Remove(four, Id(0), seed: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Is.EqualTo("A schedule needs at least 4 players, so Ann T. can't be removed."));
            Assert.That(result.Schedule, Is.SameAs(four));
        }
    }

    [Test]
    public void Remove_PlayerNotInTheSchedule_IsRefused()
    {
        var result = ScheduleRoster.Remove(Eight(), Guid.NewGuid(), seed: 1);

        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public void Remove_EveryRoundComplete_ChangesNothing()
    {
        var schedule = AllComplete(Eight());

        var result = ScheduleRoster.Remove(schedule, Id(0), seed: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Message, Is.EqualTo("Removed Ann T."));
            Assert.That(result.Schedule.Rounds, Is.EqualTo(schedule.Rounds));
        }
    }

    [Test]
    public void Remove_OtherPlayerPinned_KeepsThePinInTheSamePlace()
    {
        var schedule = Eight();
        var rounds = schedule.Rounds.ToList();
        rounds[1] = rounds[1] with { Pinned = [Id(0)] };

        var result = ScheduleRoster.Remove(schedule with { Rounds = rounds }, Id(7), seed: 5);

        var round = result.Schedule.Rounds[1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(round.Pinned, Is.EqualTo([Id(0)]));
            Assert.That(round.Games[0].Team1[0], Is.EqualTo(Id(0)));
        }
    }

    [Test]
    public void Remove_PlayerWithAStartedRound_KeepsTheRoundStarted()
    {
        var schedule = Eight();
        var rounds = schedule.Rounds.ToList();
        rounds[1] = rounds[1] with { IsStarted = true };

        var result = ScheduleRoster.Remove(schedule with { Rounds = rounds }, Id(7), seed: 5);

        Assert.That(result.Schedule.Rounds[1].IsStarted, Is.True);
    }

    [Test]
    public void Add_NewPlayer_PlaysInEveryOpenRoundAndIsShownInBlue()
    {
        var newcomer = new SchedulePlayer(Guid.NewGuid(), "Ivy", "Test", null);
        var schedule = Eight(firstRoundComplete: true);

        foreach (var seed in Seeds)
        {
            var result = ScheduleRoster.Add(schedule, newcomer, seed);

            // Ivy missed round 1, so she counts it as sitting out and never sits out in the rounds still to play.
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Succeeded, Is.True, $"seed {seed}");
                Assert.That(result.Message, Is.EqualTo("Added Ivy T. to rounds 2–3 (shown in blue)."), $"seed {seed}");
                foreach (var index in new[] { 1, 2 })
                {
                    var round = result.Schedule.Rounds[index];
                    Assert.That(round.SittingOut, Does.Not.Contain(newcomer.Id), $"seed {seed} round {index + 1}");
                    Assert.That(round.Changed, Is.EqualTo([newcomer.Id]), $"seed {seed} round {index + 1}");
                    Assert.That(PlayersIn(round), Has.Count.EqualTo(9).And.Contain(newcomer.Id));
                }
            }
        }
    }

    [Test]
    public void Add_NewPlayer_CompletedRoundIsLeftAsItWasAndNobodyPartnersAgain()
    {
        var newcomer = new SchedulePlayer(Guid.NewGuid(), "Ivy", "Test", null);
        var schedule = Eight(firstRoundComplete: true);

        foreach (var seed in Seeds)
        {
            var result = ScheduleRoster.Add(schedule, newcomer, seed);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Schedule.Rounds[0], Is.EqualTo(schedule.Rounds[0]), $"seed {seed}");
                Assert.That(PlayersIn(result.Schedule.Rounds[0]), Does.Not.Contain(newcomer.Id), $"seed {seed}");
                Assert.That(RepeatedPartnerships(result.Schedule.Rounds), Is.Zero, $"seed {seed}");
                Assert.That(result.Schedule.Players, Does.Contain(newcomer));
            }
        }
    }

    [Test]
    public void Add_ToAnEvenSessionWithNothingComplete_SharesSittingOutFairly()
    {
        var newcomer = new SchedulePlayer(Guid.NewGuid(), "Ivy", "Test", null);

        foreach (var seed in Seeds)
        {
            var result = ScheduleRoster.Add(Eight(), newcomer, seed);

            var sits = result.Schedule.Players.ToDictionary(player => player.Id, player => result.Schedule.Rounds.Count(round => round.SittingOut.Contains(player.Id)));
            Assert.That(sits.Values.Max() - sits.Values.Min(), Is.LessThanOrEqualTo(1), $"seed {seed}");
        }
    }

    [Test]
    public void Add_PlayerAlreadyInTheSchedule_IsRefused()
    {
        var result = ScheduleRoster.Add(Eight(), _players[0], seed: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Is.EqualTo("Ann Test is already in the schedule."));
        }
    }

    [Test]
    public void Add_EveryRoundComplete_HasNowhereToAdd()
    {
        var result = ScheduleRoster.Add(AllComplete(Eight()), new SchedulePlayer(Guid.NewGuid(), "Ivy", "Test", null), seed: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Is.EqualTo("Every round is complete, so there's nowhere to add Ivy Test."));
        }
    }

    [Test]
    public void Add_PlayerRemovedEarlier_CanComeBack()
    {
        var schedule = Eight(firstRoundComplete: true);
        var removed = ScheduleRoster.Remove(schedule, Id(7), seed: 1).Schedule;

        var result = ScheduleRoster.Add(removed, _players[7], seed: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(PlayersIn(result.Schedule.Rounds[1]), Does.Contain(Id(7)));
            Assert.That(result.Schedule.Players.Count(player => player.Id == Id(7)), Is.EqualTo(1));
        }
    }

    [Test]
    public void Add_PinnedPlayer_KeepsThePinInTheSamePlace()
    {
        var schedule = Eight();
        var rounds = schedule.Rounds.ToList();
        rounds[1] = rounds[1] with { Pinned = [Id(0)] };

        var result = ScheduleRoster.Add(schedule with { Rounds = rounds }, new SchedulePlayer(Guid.NewGuid(), "Ivy", "Test", null), seed: 6);

        var round = result.Schedule.Rounds[1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(round.Pinned, Is.EqualTo([Id(0)]));
            Assert.That(round.Games[0].Team1[0], Is.EqualTo(Id(0)));
        }
    }

    [Test]
    public void Continue_AfterTwoRoundsOfAnyPlayerCount_NobodyPartnersTwiceAndSharesStayEven()
    {
        var failures = new List<string>();
        for (var count = 5; count <= 14; count++)
        {
            for (var courts = 1; courts <= 4; courts++)
            {
                foreach (var seed in Seeds.Take(3))
                {
                    var people = TestPlayers.Mixed(count);
                    var first = ScheduleEngine.Generate(people, courts, seed).Take(2).ToList();

                    var next = ScheduleEngine.Continue(people, courts, 3, first, seed);
                    var all = first.Concat(next).ToList();

                    var limit = count is 6 or 7 && courts >= 2 ? 3 : 2;
                    failures.AddRange(ScheduleChecker.Check(people, courts, all, limit, rankRule: true)
                        .Select(problem => $"players={count} courts={courts} seed={seed}: {problem}"));
                }
            }
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures.Take(10)));
    }

    [Test]
    public void Continue_NoHistory_BuildsTheRequestedNumberOfRounds()
    {
        var rounds = ScheduleEngine.Continue(TestPlayers.Mixed(9), 2, 4, [], seed: 1);

        Assert.That(rounds, Has.Count.EqualTo(4));
    }

    [Test]
    public void Continue_SameSeed_GivesTheSameRounds()
    {
        var people = TestPlayers.Mixed(10);

        var first = ScheduleEngine.Continue(people, 2, 3, [], seed: 9);
        var second = ScheduleEngine.Continue(people, 2, 3, [], seed: 9);

        Assert.That(second.Select(Layout), Is.EqualTo(first.Select(Layout)));
    }

    [Test]
    public void Continue_FewerThanFourPlayers_Throws() =>
        Assert.Throws<ArgumentException>(() => ScheduleEngine.Continue(TestPlayers.Mixed(3), 1, 1, [], seed: 1));

    [Test]
    public void Continue_NoCourts_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ScheduleEngine.Continue(TestPlayers.Mixed(8), 0, 1, [], seed: 1));
}
