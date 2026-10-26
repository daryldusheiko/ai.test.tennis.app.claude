using SocialTennis.Scheduling;

namespace SocialTennis.Tests.Scheduling;

public class ScheduleEditorTests
{
    private static readonly string[] Names = ["Ann", "Bob", "Cat", "Dan", "Eve", "Fay", "Gus", "Hal"];

    private List<SchedulePlayer> _players = [];

    [SetUp]
    public void CreatePlayers() =>
        _players = [.. Names.Select(name => new SchedulePlayer(Guid.NewGuid(), name, "Test", null))];

    private Guid Id(int index) => _players[index].Id;

    private PlayerPair[] Pairs(params (int, int)[] indexes) => [.. indexes.Select(pair => PlayerPair.Of(Id(pair.Item1), Id(pair.Item2)))];

    private Guid[] Ids(params int[] indexes) => [.. indexes.Select(Id)];

    private Game DoublesGame(int a, int b, int c, int d, bool complete = false) => new([Id(a), Id(b)], [Id(c), Id(d)], complete);

    /// <summary>Eight players on two courts, three rounds, nobody partners twice. Round 2 can sit players out when given.</summary>
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

    /// <summary>Six players on one court: four play and two sit out, so a sitting player can be swapped in.</summary>
    private Schedule SixOnOneCourt() => new(
        Guid.NewGuid(),
        ScheduleStep.Review,
        [1],
        [.. _players.Take(6)],
        [new Round([DoublesGame(0, 1, 2, 3)], [Id(4), Id(5)])],
        1);

    private static List<(Guid First, Guid Second)> RepeatedPartners(Schedule schedule)
    {
        var counts = new Dictionary<(Guid First, Guid Second), int>();
        foreach (var round in schedule.Rounds)
        {
            foreach (var team in round.Games.SelectMany(game => new[] { game.Team1, game.Team2 }).Where(team => team.Count == 2))
            {
                var key = team[0].CompareTo(team[1]) < 0 ? (team[0], team[1]) : (team[1], team[0]);
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }

        return [.. counts.Where(entry => entry.Value > 1).Select(entry => entry.Key)];
    }

    private static HashSet<Guid> PlayersIn(Round round) =>
        [.. round.Games.SelectMany(game => game.Team1.Concat(game.Team2)), .. round.SittingOut];

    [Test]
    public void OptionsFor_PlayerInGame_OffersPinAndSwap()
    {
        var options = ScheduleEditor.OptionsFor(Eight(), new PlayerSpot(1, Id(0)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.Message, Is.EqualTo("Round 2."));
            Assert.That((options.CanPin, options.CanUnpin, options.CanSwap), Is.EqualTo((true, false, true)));
        }
    }

    [Test]
    public void OptionsFor_PinnedPlayer_OffersOnlyUnpin()
    {
        var pinned = ScheduleEditor.Pin(Eight(), new PlayerSpot(1, Id(0))).Schedule;

        var options = ScheduleEditor.OptionsFor(pinned, new PlayerSpot(1, Id(0)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.Message, Is.EqualTo("Pinned in this game: re-scheduling won't move them and they can't be swapped."));
            Assert.That((options.CanPin, options.CanUnpin, options.CanSwap), Is.EqualTo((false, true, false)));
        }
    }

    [Test]
    public void OptionsFor_PlayerSittingOut_OffersSwap()
    {
        var options = ScheduleEditor.OptionsFor(SixOnOneCourt(), new PlayerSpot(0, Id(4)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.Message, Is.EqualTo("Sitting out in round 1."));
            Assert.That((options.CanPin, options.CanUnpin, options.CanSwap), Is.EqualTo((false, false, true)));
        }
    }

    [Test]
    public void OptionsFor_PlayerInCompletedGame_OffersNothing()
    {
        var schedule = Eight();
        var rounds = schedule.Rounds.ToList();
        rounds[1] = rounds[1] with { Games = [rounds[1].Games[0] with { IsComplete = true }, rounds[1].Games[1]] };

        var options = ScheduleEditor.OptionsFor(schedule with { Rounds = rounds }, new PlayerSpot(1, Id(0)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.Message, Is.EqualTo("This game is complete, so it can't be changed."));
            Assert.That(options.HasOptions, Is.False);
        }
    }

    [Test]
    public void OptionsFor_PlayerInCompletedRound_OffersNothing()
    {
        var options = ScheduleEditor.OptionsFor(Eight(firstRoundComplete: true), new PlayerSpot(0, Id(0)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.Message, Is.EqualTo("Round 1 is complete, so nothing in it can be changed."));
            Assert.That(options.HasOptions, Is.False);
        }
    }

    [Test]
    public void Pin_PlayerInGame_PinsThemInThatRoundOnly()
    {
        var result = ScheduleEditor.Pin(Eight(), new PlayerSpot(1, Id(0)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Message, Is.EqualTo("Pinned Ann T."));
            Assert.That(result.Schedule.Rounds[1].Pinned, Is.EqualTo(Ids(0)));
            Assert.That(result.Schedule.Rounds[0].Pinned, Is.Empty);
        }
    }

    [Test]
    public void Unpin_PinnedPlayer_RemovesThePin()
    {
        var pinned = ScheduleEditor.Pin(Eight(), new PlayerSpot(1, Id(0))).Schedule;

        var result = ScheduleEditor.Unpin(pinned, new PlayerSpot(1, Id(0)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Schedule.Rounds[1].Pinned, Is.Empty);
        }
    }

    [Test]
    public void Pin_PlayerInCompletedRound_IsRefused()
    {
        var schedule = Eight(firstRoundComplete: true);

        var result = ScheduleEditor.Pin(schedule, new PlayerSpot(0, Id(0)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Schedule, Is.SameAs(schedule));
        }
    }

    [Test]
    public void Swap_TwoPlayersInDifferentGames_ExchangesThemAndMarksThemChanged()
    {
        var result = ScheduleEditor.Swap(Eight(), new PlayerSpot(2, Id(0)), new PlayerSpot(2, Id(2)));

        var round = result.Schedule.Rounds[2];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(round.Games[0].Team1, Is.EqualTo(Ids(2, 4)));
            Assert.That(round.Games[1].Team1, Is.EqualTo(Ids(0, 6)));
            Assert.That(round.Changed, Is.EquivalentTo(Ids(0, 2)));
            Assert.That(result.Schedule.Rounds[1].Changed, Is.Empty);
        }
    }

    [Test]
    public void Swap_PlayerSittingOutWithPlayerInGame_BringsTheSitterIn()
    {
        var result = ScheduleEditor.Swap(SixOnOneCourt(), new PlayerSpot(0, Id(4)), new PlayerSpot(0, Id(0)));

        var round = result.Schedule.Rounds[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(round.Games[0].Team1, Is.EqualTo(Ids(4, 1)));
            Assert.That(round.SittingOut, Is.EquivalentTo(Ids(0, 5)));
        }
    }

    [Test]
    public void Swap_PlayerInAnotherRound_AsksForAPlayerInThisRound()
    {
        var result = ScheduleEditor.Swap(Eight(), new PlayerSpot(2, Id(0)), new PlayerSpot(1, Id(2)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Is.EqualTo("Pick a player in round 3."));
        }
    }

    [Test]
    public void Swap_WithPinnedPlayer_IsRefused()
    {
        var pinned = ScheduleEditor.Pin(Eight(), new PlayerSpot(2, Id(2))).Schedule;

        var result = ScheduleEditor.Swap(pinned, new PlayerSpot(2, Id(0)), new PlayerSpot(2, Id(2)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Is.EqualTo("Cat T. is pinned and can't be swapped."));
        }
    }

    [Test]
    public void Swap_WithPlayerInCompletedGame_IsRefused()
    {
        var schedule = Eight();
        var rounds = schedule.Rounds.ToList();
        rounds[2] = rounds[2] with { Games = [rounds[2].Games[0], rounds[2].Games[1] with { IsComplete = true }] };

        var result = ScheduleEditor.Swap(schedule with { Rounds = rounds }, new PlayerSpot(2, Id(0)), new PlayerSpot(2, Id(2)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Is.EqualTo("That game is complete, so it can't be changed."));
        }
    }

    [Test]
    public void Swap_PlayersOnTheSameSide_IsRefused()
    {
        var result = ScheduleEditor.Swap(Eight(), new PlayerSpot(2, Id(0)), new PlayerSpot(2, Id(4)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Is.EqualTo("Ann T. and Eve T. are already on the same side."));
        }
    }

    [Test]
    public void Swap_PlayersOnOppositeSidesOfAGame_Swaps()
    {
        var result = ScheduleEditor.Swap(Eight(), new PlayerSpot(2, Id(0)), new PlayerSpot(2, Id(1)));

        Assert.That(result.Schedule.Rounds[2].Games[0].Team1, Is.EqualTo(Ids(1, 4)));
    }

    [Test]
    public void RepeatPairs_NobodyPartnersTwice_IsEmpty() =>
        Assert.That(ScheduleEditor.RepeatPairs(Eight()), Is.Empty);

    [Test]
    public void Swap_CreatingASecondPartnership_ShowsTheirNamesInRed()
    {
        // Swapping Bob and Cat in round 2 makes Ann & Bob and Cat & Dan partners again.
        var result = ScheduleEditor.Swap(Eight(), new PlayerSpot(1, Id(1)), new PlayerSpot(1, Id(2)));

        var repeats = ScheduleEditor.RepeatPairs(result.Schedule);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(repeats.Select(repeat => repeat.Pair), Is.EquivalentTo(Pairs((0, 1), (2, 3))));
            Assert.That(repeats.All(repeat => repeat.Rounds.Count == 2 && repeat.Rounds[0] == 0 && repeat.Rounds[1] == 1), Is.True);
            Assert.That(result.Message, Does.EndWith("Partners who play together twice are shown in red."));
        }
    }

    [Test]
    public void RepeatPairs_AcceptedPair_IsNotListed()
    {
        var swapped = ScheduleEditor.Swap(Eight(), new PlayerSpot(1, Id(1)), new PlayerSpot(1, Id(2))).Schedule;

        var accepted = ScheduleEditor.AcceptPair(swapped, PlayerPair.Of(Id(0), Id(1))).Schedule;

        Assert.That(ScheduleEditor.RepeatPairs(accepted).Select(repeat => repeat.Pair), Is.EqualTo(Pairs((2, 3))));
    }

    [Test]
    public void Reschedule_NothingRed_ChangesNothing()
    {
        var schedule = Eight();

        var result = ScheduleEditor.Reschedule(schedule);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Message, Is.EqualTo("Nothing needed changing."));
            Assert.That(result.Schedule.Rounds, Is.EqualTo(schedule.Rounds));
        }
    }

    [Test]
    public void Reschedule_RepeatPartnersAfterASwap_SeparatesThemWithoutTouchingTheUsersSwap()
    {
        var swapped = ScheduleEditor.Swap(Eight(), new PlayerSpot(1, Id(1)), new PlayerSpot(1, Id(2))).Schedule;

        var result = ScheduleEditor.Reschedule(swapped);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(RepeatedPartners(result.Schedule), Is.Empty);
            Assert.That(result.Schedule.Rounds[1].Games, Is.EqualTo(swapped.Rounds[1].Games));
            Assert.That(result.Message, Does.Match(@"^Re-scheduled: \d+ players moved \(shown in blue\)\.$"));
            Assert.That(result.Schedule.Rounds[0].Changed, Is.Not.Empty);
        }
    }

    [Test]
    public void Reschedule_MovesPlayersOnlyWithinTheirRound()
    {
        var swapped = ScheduleEditor.Swap(Eight(), new PlayerSpot(1, Id(1)), new PlayerSpot(1, Id(2))).Schedule;

        var result = ScheduleEditor.Reschedule(swapped);

        for (var round = 0; round < swapped.Rounds.Count; round++)
        {
            Assert.That(PlayersIn(result.Schedule.Rounds[round]), Is.EquivalentTo(PlayersIn(swapped.Rounds[round])));
        }
    }

    [Test]
    public void Reschedule_RepeatPlayersPinned_ReportsThePinnedPlayersAreInTheWay()
    {
        var swapped = ScheduleEditor.Swap(Eight(), new PlayerSpot(1, Id(1)), new PlayerSpot(1, Id(2))).Schedule;
        var pinned = swapped;
        foreach (var round in new[] { 0, 1 })
        {
            foreach (var player in new[] { 0, 1, 2, 3 })
            {
                pinned = ScheduleEditor.Pin(pinned, new PlayerSpot(round, Id(player))).Schedule;
            }
        }

        var result = ScheduleEditor.Reschedule(pinned);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Message, Does.StartWith("Couldn't separate "));
            Assert.That(result.Message, Does.EndWith(": pinned players are in the way."));
            Assert.That(result.Schedule.Rounds, Is.EqualTo(pinned.Rounds));
        }
    }

    [Test]
    public void Reschedule_PinnedPlayer_StaysWhereTheyAre()
    {
        var swapped = ScheduleEditor.Swap(Eight(), new PlayerSpot(1, Id(1)), new PlayerSpot(1, Id(2))).Schedule;
        var pinned = ScheduleEditor.Pin(swapped, new PlayerSpot(0, Id(1))).Schedule;

        var result = ScheduleEditor.Reschedule(pinned);

        Assert.That(result.Schedule.Rounds[0].Games[0].Team1, Does.Contain(Id(1)));
    }

    [Test]
    public void Reschedule_SittingOutShare_StaysFair()
    {
        // Eight players on one court: four sit out each round. A swap that causes a repeat must not give one player extra sit-outs.
        var generated = ScheduleFlow.Generate(
            ScheduleFlow.WithPlayers(ScheduleFlow.WithCourts(ScheduleFlow.Start(Guid.NewGuid()), [1]), _players),
            [],
            5);
        var round = generated.Rounds[1];
        var swap = ScheduleEditor.Swap(generated, new PlayerSpot(1, round.Games[0].Team1[0]), new PlayerSpot(1, round.SittingOut[0]));

        var result = ScheduleEditor.Reschedule(swap.Schedule);

        var sat = _players.Select(player => result.Schedule.Rounds.Count(r => r.SittingOut.Contains(player.Id))).ToList();
        var before = _players.Select(player => swap.Schedule.Rounds.Count(r => r.SittingOut.Contains(player.Id))).ToList();
        Assert.That(sat.Max() - sat.Min(), Is.LessThanOrEqualTo(Math.Max(1, before.Max() - before.Min())));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void Reschedule_GeneratedScheduleWithASwap_KeepsEveryoneInOneGamePerRoundAndNeverAddsRepeats(int seed)
    {
        var generated = ScheduleFlow.Generate(
            ScheduleFlow.WithPlayers(ScheduleFlow.WithCourts(ScheduleFlow.Start(Guid.NewGuid()), [1, 2]), _players),
            [],
            seed);
        var round = generated.Rounds[2];
        var swapped = ScheduleEditor.Swap(generated, new PlayerSpot(2, round.Games[0].Team1[0]), new PlayerSpot(2, round.Games[1].Team2[0])).Schedule;

        var result = ScheduleEditor.Reschedule(swapped);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(RepeatedPartners(result.Schedule), Has.Count.LessThanOrEqualTo(RepeatedPartners(swapped).Count));
            foreach (var r in result.Schedule.Rounds)
            {
                var everyone = r.Games.SelectMany(game => game.Team1.Concat(game.Team2)).Concat(r.SittingOut).ToList();
                Assert.That(everyone, Is.EquivalentTo(_players.Select(player => player.Id)));
            }
        }
    }

    [Test]
    public void PendingChoices_RepeatInvolvingACompletedGame_IsListed()
    {
        // Round 1 is complete with Ann & Bob as partners; swapping in round 2 makes them partners again.
        var swapped = ScheduleEditor.Swap(Eight(firstRoundComplete: true), new PlayerSpot(1, Id(1)), new PlayerSpot(1, Id(2))).Schedule;

        var pending = ScheduleEditor.PendingChoices(swapped);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pending.Select(choice => choice.Pair), Is.EquivalentTo(Pairs((0, 1), (2, 3))));
            Assert.That(pending.All(choice => choice.CompletedRound == 0 && choice.LaterRound == 1), Is.True);
        }
    }

    [Test]
    public void Reschedule_RepeatInvolvingACompletedGame_LeavesItForTheUserToDecide()
    {
        var swapped = ScheduleEditor.Swap(Eight(firstRoundComplete: true), new PlayerSpot(1, Id(1)), new PlayerSpot(1, Id(2))).Schedule;

        var result = ScheduleEditor.Reschedule(swapped);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Schedule.Rounds, Is.EqualTo(swapped.Rounds));
            Assert.That(result.Message, Does.Contain("completed round"));
        }
    }

    [Test]
    public void MoveToResolve_ChosenPlayer_LeavesTheLaterGameAndNothingInTheCompletedRoundChanges()
    {
        var swapped = ScheduleEditor.Swap(Eight(firstRoundComplete: true), new PlayerSpot(1, Id(1)), new PlayerSpot(1, Id(2))).Schedule;
        var choice = ScheduleEditor.PendingChoices(swapped).First(pending => pending.Pair == PlayerPair.Of(Id(0), Id(1)));

        var result = ScheduleEditor.MoveToResolve(swapped, choice, Id(1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Message, Is.EqualTo("Moved Bob T. in round 2 (shown in blue)."));
            Assert.That(ScheduleEditor.RepeatPairs(result.Schedule).Select(repeat => repeat.Pair), Does.Not.Contain(PlayerPair.Of(Id(0), Id(1))));
            Assert.That(result.Schedule.Rounds[0], Is.EqualTo(swapped.Rounds[0]));
        }
    }

    [Test]
    public void MoveToResolve_ChosenPlayerPinned_IsRefused()
    {
        var swapped = ScheduleEditor.Swap(Eight(firstRoundComplete: true), new PlayerSpot(1, Id(1)), new PlayerSpot(1, Id(2))).Schedule;
        var pinned = ScheduleEditor.Pin(swapped, new PlayerSpot(1, Id(1))).Schedule;
        var choice = ScheduleEditor.PendingChoices(pinned).First(pending => pending.Pair == PlayerPair.Of(Id(0), Id(1)));

        var result = ScheduleEditor.MoveToResolve(pinned, choice, Id(1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Is.EqualTo("Bob T. is pinned and can't be moved."));
        }
    }

    [Test]
    public void AcceptPair_KeepTogether_StopsRescheduleFromMovingThem()
    {
        var swapped = ScheduleEditor.Swap(Eight(firstRoundComplete: true), new PlayerSpot(1, Id(1)), new PlayerSpot(1, Id(2))).Schedule;
        var accepted = swapped;
        foreach (var choice in ScheduleEditor.PendingChoices(swapped))
        {
            accepted = ScheduleEditor.AcceptPair(accepted, choice.Pair).Schedule;
        }

        var result = ScheduleEditor.Reschedule(accepted);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ScheduleEditor.RepeatPairs(accepted), Is.Empty);
            Assert.That(ScheduleEditor.PendingChoices(accepted), Is.Empty);
            Assert.That(result.Message, Is.EqualTo("Nothing needed changing."));
            Assert.That(result.Schedule.Rounds, Is.EqualTo(accepted.Rounds));
        }
    }

    [Test]
    public void ClearChanged_BlueMarkers_AreRemovedButPinsStay()
    {
        var swapped = ScheduleEditor.Swap(Eight(), new PlayerSpot(2, Id(0)), new PlayerSpot(2, Id(2))).Schedule;
        var pinned = ScheduleEditor.Pin(swapped, new PlayerSpot(1, Id(0))).Schedule;

        var cleared = ScheduleEditor.ClearChanged(pinned);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cleared.Rounds.SelectMany(round => round.Changed), Is.Empty);
            Assert.That(cleared.Rounds[1].Pinned, Is.EqualTo(Ids(0)));
        }
    }

    [Test]
    public void OptionsFor_PlayerInGame_OffersRemoveFromSchedule() =>
        Assert.That(ScheduleEditor.OptionsFor(Eight(), new PlayerSpot(1, Id(0))).CanRemove, Is.True);

    [Test]
    public void OptionsFor_PinnedPlayer_OffersRemoveFromSchedule()
    {
        var pinned = ScheduleEditor.Pin(Eight(), new PlayerSpot(1, Id(0))).Schedule;

        Assert.That(ScheduleEditor.OptionsFor(pinned, new PlayerSpot(1, Id(0))).CanRemove, Is.True);
    }

    [Test]
    public void OptionsFor_PlayerSittingOut_OffersRemoveFromSchedule() =>
        Assert.That(ScheduleEditor.OptionsFor(SixOnOneCourt(), new PlayerSpot(0, Id(4))).CanRemove, Is.True);

    [Test]
    public void OptionsFor_PlayerInCompletedRound_DoesNotOfferRemove() =>
        Assert.That(ScheduleEditor.OptionsFor(Eight(firstRoundComplete: true), new PlayerSpot(0, Id(0))).CanRemove, Is.False);
}
