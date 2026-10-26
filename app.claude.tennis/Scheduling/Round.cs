namespace SocialTennis.Scheduling;

/// <summary>
/// One round. <see cref="Pinned"/> players keep their place when the schedule is re-calculated; <see cref="Changed"/> players
/// were moved by the user, by Re-schedule or by adding a player, and are shown in blue until the user taps Done.
/// </summary>
public sealed record Round(IReadOnlyList<Game> Games, IReadOnlyList<Guid> SittingOut)
{
    public IReadOnlyList<Guid> Pinned { get; init; } = [];

    public IReadOnlyList<Guid> Changed { get; init; } = [];

    /// <summary>The round has been started and is being played. Saved so it survives closing the app.</summary>
    public bool IsStarted { get; init; }

    public RoundStatus Status => IsComplete ? RoundStatus.Complete : IsStarted ? RoundStatus.InPlay : RoundStatus.NotStarted;

    public bool IsComplete => Games.Count > 0 && Games.All(game => game.IsComplete);
}
