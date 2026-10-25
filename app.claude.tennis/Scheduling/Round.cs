namespace SocialTennis.Scheduling;

public sealed record Round(IReadOnlyList<Game> Games, IReadOnlyList<Guid> SittingOut)
{
    public bool IsComplete => Games.Count > 0 && Games.All(game => game.IsComplete);
}
