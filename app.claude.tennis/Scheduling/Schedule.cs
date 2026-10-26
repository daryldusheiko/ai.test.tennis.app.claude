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

    /// <summary>Edit mode is on. Saved, because a new schedule stays in edit mode, even if the app is closed, until Done or Play.</summary>
    public bool IsEditing { get; init; }
}
