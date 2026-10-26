namespace SocialTennis.Scheduling;

/// <summary>The one schedule. Courts are kept in ascending order: game 1 is on the lowest court.</summary>
public sealed record Schedule(
    Guid Id,
    ScheduleStep Step,
    IReadOnlyList<int> Courts,
    IReadOnlyList<SchedulePlayer> Players,
    IReadOnlyList<Round> Rounds,
    int Seed)
{
    /// <summary>Pairs the user chose to keep together although they partner more than once.</summary>
    public IReadOnlyList<PlayerPair> AcceptedPairs { get; init; } = [];
}
