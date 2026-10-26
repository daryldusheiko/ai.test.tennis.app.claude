namespace SocialTennis.Scheduling;

/// <summary>Starting, finishing and cancelling rounds. Any round can start, in any order, but only one is in play at a time.</summary>
public static class RoundProgress
{
    /// <summary>The index of the round in play, or null when none is.</summary>
    public static int? InPlayIndex(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        for (var index = 0; index < schedule.Rounds.Count; index++)
        {
            if (schedule.Rounds[index].Status == RoundStatus.InPlay)
            {
                return index;
            }
        }

        return null;
    }

    /// <summary>Starts a round. Nothing changes when it is complete, already started, or another round is in play.</summary>
    public static Schedule Start(Schedule schedule, int index)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return IsValid(schedule, index) && schedule.Rounds[index].Status == RoundStatus.NotStarted && InPlayIndex(schedule) is null
            ? Replace(schedule, index, schedule.Rounds[index] with { IsStarted = true })
            : schedule;
    }

    /// <summary>Marks every game of the round in play as complete.</summary>
    public static Schedule Finish(Schedule schedule, int index)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (!IsValid(schedule, index) || schedule.Rounds[index].Status != RoundStatus.InPlay)
        {
            return schedule;
        }

        var round = schedule.Rounds[index];
        return Replace(schedule, index, round with { IsStarted = false, Games = [.. round.Games.Select(game => game with { IsComplete = true })] });
    }

    /// <summary>Returns the round in play to not started.</summary>
    public static Schedule Cancel(Schedule schedule, int index)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return IsValid(schedule, index) && schedule.Rounds[index].Status == RoundStatus.InPlay
            ? Replace(schedule, index, schedule.Rounds[index] with { IsStarted = false })
            : schedule;
    }

    private static bool IsValid(Schedule schedule, int index) => index >= 0 && index < schedule.Rounds.Count;

    private static Schedule Replace(Schedule schedule, int index, Round round)
    {
        var rounds = schedule.Rounds.ToList();
        rounds[index] = round;
        return schedule with { Rounds = rounds };
    }
}
