using SocialTennis.Scheduling;
using SocialTennis.Tests.Players;

namespace SocialTennis.Tests.Scheduling;

public class RoundProgressTests
{
    private static Game Doubles(List<SchedulePlayer> people, int a, int b, int c, int d) =>
        new([people[a].Id, people[b].Id], [people[c].Id, people[d].Id], false);

    private static Schedule ThreeRounds()
    {
        var people = TestPlayers.Mixed(8);
        return new Schedule(
            Guid.NewGuid(),
            ScheduleStep.InPlay,
            [1, 2],
            people,
            [
                new Round([Doubles(people, 0, 1, 2, 3), Doubles(people, 4, 5, 6, 7)], []),
                new Round([Doubles(people, 0, 2, 4, 6), Doubles(people, 1, 3, 5, 7)], []),
                new Round([Doubles(people, 0, 4, 1, 5), Doubles(people, 2, 6, 3, 7)], []),
            ],
            1);
    }

    [Test]
    public void Status_NewRound_IsNotStarted() =>
        Assert.That(ThreeRounds().Rounds[0].Status, Is.EqualTo(RoundStatus.NotStarted));

    [Test]
    public void Start_AnyRoundInAnyOrder_PutsOnlyThatRoundInPlay()
    {
        var schedule = RoundProgress.Start(ThreeRounds(), 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(schedule.Rounds[2].Status, Is.EqualTo(RoundStatus.InPlay));
            Assert.That(schedule.Rounds[0].Status, Is.EqualTo(RoundStatus.NotStarted));
            Assert.That(RoundProgress.InPlayIndex(schedule), Is.EqualTo(2));
        }
    }

    [Test]
    public void Start_AnotherRoundInPlay_ChangesNothing()
    {
        var started = RoundProgress.Start(ThreeRounds(), 0);

        var again = RoundProgress.Start(started, 1);

        Assert.That(again, Is.SameAs(started));
    }

    [Test]
    public void Start_CompleteRound_ChangesNothing()
    {
        var finished = RoundProgress.Finish(RoundProgress.Start(ThreeRounds(), 0), 0);

        Assert.That(RoundProgress.Start(finished, 0), Is.SameAs(finished));
    }

    [Test]
    public void Start_IndexOutOfRange_ChangesNothing()
    {
        var schedule = ThreeRounds();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(RoundProgress.Start(schedule, -1), Is.SameAs(schedule));
            Assert.That(RoundProgress.Start(schedule, 3), Is.SameAs(schedule));
        }
    }

    [Test]
    public void Finish_RoundInPlay_MarksEveryGameCompleteAndNothingIsInPlay()
    {
        var schedule = RoundProgress.Finish(RoundProgress.Start(ThreeRounds(), 1), 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(schedule.Rounds[1].Status, Is.EqualTo(RoundStatus.Complete));
            Assert.That(schedule.Rounds[1].Games, Has.All.Matches<Game>(game => game.IsComplete));
            Assert.That(RoundProgress.InPlayIndex(schedule), Is.Null);
        }
    }

    [Test]
    public void Finish_RoundNotStarted_ChangesNothing()
    {
        var schedule = ThreeRounds();

        Assert.That(RoundProgress.Finish(schedule, 0), Is.SameAs(schedule));
    }

    [Test]
    public void Cancel_RoundInPlay_ReturnsItToNotStartedKeepingGamesIncomplete()
    {
        var schedule = RoundProgress.Cancel(RoundProgress.Start(ThreeRounds(), 0), 0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(schedule.Rounds[0].Status, Is.EqualTo(RoundStatus.NotStarted));
            Assert.That(schedule.Rounds[0].Games, Has.None.Matches<Game>(game => game.IsComplete));
        }
    }

    [Test]
    public void Cancel_CompleteRound_ChangesNothing()
    {
        var finished = RoundProgress.Finish(RoundProgress.Start(ThreeRounds(), 0), 0);

        Assert.That(RoundProgress.Cancel(finished, 0), Is.SameAs(finished));
    }

    [Test]
    public void Start_AfterAnotherIsFinished_AllowsTheNextRound()
    {
        var schedule = RoundProgress.Finish(RoundProgress.Start(ThreeRounds(), 0), 0);

        schedule = RoundProgress.Start(schedule, 2);

        Assert.That(schedule.Rounds[2].Status, Is.EqualTo(RoundStatus.InPlay));
    }

    [Test]
    public async Task SaveAsync_RoundInPlay_StaysInPlayWhenLoaded()
    {
        var service = new ScheduleService(new InMemoryStorage());

        await service.SaveAsync(RoundProgress.Start(ThreeRounds(), 1));
        var loaded = await service.GetAsync();

        Assert.That(loaded?.Rounds[1].Status, Is.EqualTo(RoundStatus.InPlay));
    }
}
