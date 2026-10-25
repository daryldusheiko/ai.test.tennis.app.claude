using SocialTennis.Scheduling;

namespace SocialTennis.Tests.Scheduling;

public class ScheduleEngineTests
{
    private static readonly int[] Seeds = [1, 2, 3];

    private static readonly int[] OneGameEachRound = [1, 1, 1];

    private static readonly (int Side1, int Side2)[] DoublesAndSingles = [(2, 2), (1, 1)];

    private static readonly (int Side1, int Side2)[] DoublesAndThreePlayer = [(2, 2), (1, 2)];

    // Written out by hand from the spec: 4 players have 3 pairings, 5 players have 5, otherwise a session is 6 rounds.
    private static int ExpectedRounds(int players) => players switch
    {
        4 => 3,
        5 => 5,
        _ => 6,
    };

    // The spec allows three meetings where two are impossible (6 players with singles); 7 with a three-player game is the same.
    private static int OpponentLimit(int players, int courts) => players is 6 or 7 && courts >= 2 ? 3 : 2;

    [Test]
    public void Generate_EveryPlayerAndCourtCount_FollowsEverySpecRule()
    {
        var failures = new List<string>();
        for (var players = 4; players <= 30; players++)
        {
            for (var courts = 1; courts <= 8; courts++)
            {
                foreach (var seed in Seeds)
                {
                    var people = TestPlayers.Mixed(players);
                    var rounds = ScheduleEngine.Generate(people, courts, seed);
                    var problems = ScheduleChecker.Check(people, courts, rounds, OpponentLimit(players, courts), rankRule: true);
                    if (rounds.Count != ExpectedRounds(players))
                    {
                        problems.Add($"{rounds.Count} rounds, expected {ExpectedRounds(players)}");
                    }

                    failures.AddRange(problems.Select(problem => $"players={players} courts={courts} seed={seed}: {problem}"));
                }
            }
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures.Take(10)));
    }

    [Test]
    public void Generate_FourPlayers_GivesThreeRoundsOfOneGame()
    {
        var rounds = ScheduleEngine.Generate(TestPlayers.Mixed(4), 3, 7);

        Assert.That(rounds.Select(round => round.Games.Count), Is.EqualTo(OneGameEachRound));
    }

    [Test]
    public void Generate_FivePlayers_GivesFiveRoundsWithOneSittingOutEach()
    {
        var rounds = ScheduleEngine.Generate(TestPlayers.Mixed(5), 2, 7);

        Assert.That(rounds, Has.Count.EqualTo(5));
        Assert.That(rounds.Select(round => round.SittingOut.Count), Has.All.EqualTo(1));
    }

    [Test]
    public void Generate_SixPlayersTwoCourts_PlaysDoublesAndSingles()
    {
        var rounds = ScheduleEngine.Generate(TestPlayers.Mixed(6), 2, 7);

        Assert.That(rounds[0].Games.Select(game => (game.Team1.Count, game.Team2.Count)), Is.EqualTo(DoublesAndSingles));
    }

    [Test]
    public void Generate_SevenPlayersTwoCourts_PlaysDoublesAndAThreePlayerGame()
    {
        var rounds = ScheduleEngine.Generate(TestPlayers.Mixed(7), 2, 7);

        Assert.That(rounds[0].Games.Select(game => (game.Team1.Count, game.Team2.Count)), Is.EqualTo(DoublesAndThreePlayer));
    }

    [Test]
    public void Generate_SevenPlayersOneCourt_SitsThreeOut()
    {
        var rounds = ScheduleEngine.Generate(TestPlayers.Mixed(7), 1, 7);

        Assert.That(rounds.Select(round => round.SittingOut.Count), Has.All.EqualTo(3));
    }

    [Test]
    public void Generate_SameSeed_GivesTheSameSchedule()
    {
        var people = TestPlayers.Mixed(14);

        var first = ScheduleEngine.Generate(people, 3, 42);
        var second = ScheduleEngine.Generate(people, 3, 42);

        Assert.That(second.Select(Describe), Is.EqualTo(first.Select(Describe)));
    }

    [Test]
    public void Generate_DifferentSeeds_GiveDifferentSchedules()
    {
        var people = TestPlayers.Mixed(14);

        var first = ScheduleEngine.Generate(people, 3, 1);
        var second = ScheduleEngine.Generate(people, 3, 2);

        Assert.That(second.Select(Describe), Is.Not.EqualTo(first.Select(Describe)));
    }

    [Test]
    public void Generate_LoneRankOneAmongRankThrees_StillCompletesAndOnlyClashesWithTheOne()
    {
        var people = TestPlayers.WithRanks(1, 3, 3, 3, 3, 3);

        var rounds = ScheduleEngine.Generate(people, 2, 5);

        var clashes = rounds.SelectMany(round => round.Games).Where(game => game.Team1.Count == 1 && game.Team2.Count == 1)
            .Where(game => game.Team1.Concat(game.Team2).Contains(people[0].Id)).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rounds, Has.Count.EqualTo(6));
            Assert.That(ScheduleChecker.Check(people, 2, rounds, 3, rankRule: false), Is.Empty);
            Assert.That(clashes, Has.Count.EqualTo(2), "the lone 1 plays singles twice and each is against a 3");
        }
    }

    [Test]
    public void Generate_RanksThatCanBeKeptApart_NeverPutAOneAgainstAThreeInSingles()
    {
        var people = TestPlayers.WithRanks(1, 1, 3, 3, 2, 2, null, null, 1, 3);

        var failures = Seeds.SelectMany(seed => ScheduleChecker.Check(people, 3, ScheduleEngine.Generate(people, 3, seed), 2, rankRule: true)).ToList();

        Assert.That(failures, Is.Empty);
    }

    [Test]
    public void Generate_LessThanFourPlayers_Throws()
    {
        Assert.That(() => ScheduleEngine.Generate(TestPlayers.Mixed(3), 3, 1), Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void Generate_NoCourts_Throws()
    {
        Assert.That(() => ScheduleEngine.Generate(TestPlayers.Mixed(8), 0, 1), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void RoundsPossible_SmallSessions_MatchTheSpec()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ScheduleEngine.RoundsPossible(4, 2), Is.EqualTo(3));
            Assert.That(ScheduleEngine.RoundsPossible(5, 2), Is.EqualTo(5));
            Assert.That(ScheduleEngine.RoundsPossible(8, 2), Is.EqualTo(6));
        }
    }

    [Test]
    public void Generate_TwentyFourPlayersOnSixCourts_IsQuick()
    {
        var people = TestPlayers.Mixed(24);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var rounds = ScheduleEngine.Generate(people, 6, 1);
        watch.Stop();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rounds, Has.Count.EqualTo(6));
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
        }
    }

    private static string Describe(Round round) => string.Join("|", round.Games.Select(game => string.Join(",", game.Team1.Concat(game.Team2)))) + "/" + string.Join(",", round.SittingOut);
}
