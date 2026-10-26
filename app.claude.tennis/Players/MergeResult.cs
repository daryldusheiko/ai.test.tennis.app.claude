namespace SocialTennis.Players;

/// <summary>The outcome of adding players to the list: the players to keep and how many were new or already there.</summary>
public sealed record MergeResult(IReadOnlyList<Player> Players, int Added, int Skipped);
