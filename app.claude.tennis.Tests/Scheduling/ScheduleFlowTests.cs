using SocialTennis.Players;
using SocialTennis.Scheduling;

namespace SocialTennis.Tests.Scheduling;

public class ScheduleFlowTests
{
    private static readonly int[] FourFiveSix = [4, 5, 6];

    private static readonly int[] FourFive = [4, 5];

    private static Schedule WithEightPlayers(out List<SchedulePlayer> people)
    {
        people = TestPlayers.Mixed(8);
        return ScheduleFlow.WithPlayers(ScheduleFlow.WithCourts(ScheduleFlow.Start(Guid.NewGuid()), [5, 4]), people);
    }

    [Test]
    public void Start_NewSchedule_IsAtTheCourtsStepAndEmpty()
    {
        var schedule = ScheduleFlow.Start(Guid.NewGuid());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(schedule.Step, Is.EqualTo(ScheduleStep.Courts));
            Assert.That(schedule.Courts, Is.Empty);
            Assert.That(schedule.Players, Is.Empty);
            Assert.That(schedule.Rounds, Is.Empty);
        }
    }

    [Test]
    public void WithCourts_Saved_KeepsThemInOrderAndMovesToPlayers()
    {
        var schedule = ScheduleFlow.WithCourts(ScheduleFlow.Start(Guid.NewGuid()), [6, 4, 5]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(schedule.Courts, Is.EqualTo(FourFiveSix));
            Assert.That(schedule.Step, Is.EqualTo(ScheduleStep.Players));
        }
    }

    [Test]
    public void BackToCourts_WithPlayers_KeepsThePlayersAndShowsCourts()
    {
        var schedule = WithEightPlayers(out var people);

        var back = ScheduleFlow.BackToCourts(schedule, [.. people.Take(5)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(back.Step, Is.EqualTo(ScheduleStep.Courts));
            Assert.That(back.Players, Has.Count.EqualTo(5));
            Assert.That(back.Courts, Is.EqualTo(FourFive));
        }
    }

    [Test]
    public void Generate_EightPlayersTwoCourts_GivesSixUncompleteRoundsAtReview()
    {
        var schedule = WithEightPlayers(out _);

        var generated = ScheduleFlow.Generate(schedule, [], 9);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(generated.Step, Is.EqualTo(ScheduleStep.Review));
            Assert.That(generated.Rounds, Has.Count.EqualTo(6));
            Assert.That(generated.Rounds.SelectMany(round => round.Games).Any(game => game.IsComplete), Is.False);
            Assert.That(generated.Seed, Is.EqualTo(9));
        }
    }

    [Test]
    public void Generate_PlayerEditedSinceAdding_UsesTheLatestNameAndRank()
    {
        var schedule = WithEightPlayers(out var people);
        var edited = new Player(people[0].Id, "Renamed", "Person", 2, string.Empty);

        var generated = ScheduleFlow.Generate(schedule, [edited], 1);

        Assert.That(generated.Players[0], Is.EqualTo(new SchedulePlayer(people[0].Id, "Renamed", "Person", 2)));
    }

    [Test]
    public void BackToPlayers_Reviewed_KeepsThePlayersAndDropsTheRounds()
    {
        var generated = ScheduleFlow.Generate(WithEightPlayers(out _), [], 1);

        var back = ScheduleFlow.BackToPlayers(generated);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(back.Step, Is.EqualTo(ScheduleStep.Players));
            Assert.That(back.Players, Has.Count.EqualTo(8));
            Assert.That(back.Rounds, Is.Empty);
        }
    }

    [Test]
    public void Play_Reviewed_MarksItInPlay()
    {
        var generated = ScheduleFlow.Generate(WithEightPlayers(out _), [], 1);

        Assert.That(ScheduleFlow.Play(generated).Step, Is.EqualTo(ScheduleStep.InPlay));
    }

    [Test]
    public void Play_RoundsWithBlueMarkers_ClearsThemButKeepsPins()
    {
        var generated = ScheduleFlow.Generate(WithEightPlayers(out _), [], 1);
        var game = generated.Rounds[0].Games[0];
        var edited = ScheduleEditor.Pin(generated, new PlayerSpot(0, game.Team1[0])).Schedule;
        edited = ScheduleEditor.Swap(edited, new PlayerSpot(0, game.Team1[1]), new PlayerSpot(0, game.Team2[0])).Schedule;

        var playing = ScheduleFlow.Play(edited);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(edited.Rounds[0].Changed, Is.Not.Empty);
            Assert.That(playing.Rounds[0].Changed, Is.Empty);
            Assert.That(playing.Rounds[0].Pinned, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void MoveRound_ToAnotherPosition_ReordersRounds()
    {
        var generated = ScheduleFlow.Generate(WithEightPlayers(out _), [], 1);

        var moved = ScheduleFlow.MoveRound(generated, 0, 2);

        Round[] expected = [generated.Rounds[1], generated.Rounds[2], generated.Rounds[0], generated.Rounds[3], generated.Rounds[4], generated.Rounds[5]];
        Assert.That(moved.Rounds, Is.EqualTo(expected));
    }

    [Test]
    public void MoveRound_ACompletedRound_StaysWhereItIs()
    {
        var generated = ScheduleFlow.Generate(WithEightPlayers(out _), [], 1);
        var completed = generated.Rounds[0] with { Games = [.. generated.Rounds[0].Games.Select(game => game with { IsComplete = true })] };
        var schedule = generated with { Rounds = [completed, .. generated.Rounds.Skip(1)] };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ScheduleFlow.MoveRound(schedule, 0, 2), Is.SameAs(schedule));
            Assert.That(ScheduleFlow.MoveRound(schedule, 2, 0), Is.SameAs(schedule));
        }
    }

    [Test]
    public void MoveGame_WithinARound_ReordersThatRoundOnly()
    {
        var generated = ScheduleFlow.Generate(WithEightPlayers(out _), [], 1);

        var moved = ScheduleFlow.MoveGame(generated, 1, 0, 1);

        Game[] expected = [generated.Rounds[1].Games[1], generated.Rounds[1].Games[0]];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(moved.Rounds[1].Games, Is.EqualTo(expected));
            Assert.That(moved.Rounds[0], Is.SameAs(generated.Rounds[0]));
        }
    }

    [Test]
    public void MoveGame_ACompletedGame_StaysWhereItIs()
    {
        var generated = ScheduleFlow.Generate(WithEightPlayers(out _), [], 1);
        var games = generated.Rounds[0].Games.ToList();
        games[0] = games[0] with { IsComplete = true };
        var schedule = generated with { Rounds = [generated.Rounds[0] with { Games = games }, .. generated.Rounds.Skip(1)] };

        Assert.That(ScheduleFlow.MoveGame(schedule, 0, 0, 1), Is.SameAs(schedule));
    }

    [TestCase(-1, 0)]
    [TestCase(0, 6)]
    [TestCase(2, 2)]
    public void MoveRound_OutOfRangeOrNoMove_ChangesNothing(int from, int to)
    {
        var generated = ScheduleFlow.Generate(WithEightPlayers(out _), [], 1);

        Assert.That(ScheduleFlow.MoveRound(generated, from, to), Is.SameAs(generated));
    }

    [Test]
    public void MoveGame_UnknownRound_ChangesNothing()
    {
        var generated = ScheduleFlow.Generate(WithEightPlayers(out _), [], 1);

        Assert.That(ScheduleFlow.MoveGame(generated, 9, 0, 1), Is.SameAs(generated));
    }
}
