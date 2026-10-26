namespace SocialTennis.Scheduling;

/// <summary>A pair who partnered in a completed game and are partners again in a game that can still change.</summary>
public sealed record PlayedTogether(PlayerPair Pair, int CompletedRound, int LaterRound);
