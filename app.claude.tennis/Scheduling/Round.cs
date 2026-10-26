namespace SocialTennis.Scheduling;

/// <summary>
/// One round. <see cref="Pinned"/> players keep their place when the schedule is re-calculated; <see cref="Changed"/> players
/// were moved by the user, by Re-schedule or by adding a player, and are shown in blue until the user taps Done.
/// </summary>
public sealed record Round(IReadOnlyList<Game> Games, IReadOnlyList<Guid> SittingOut)
{
    public IReadOnlyList<Guid> Pinned { get; init; } = [];

    public IReadOnlyList<Guid> Changed { get; init; } = [];

    public bool IsComplete => Games.Count > 0 && Games.All(game => game.IsComplete);
}
