namespace SocialTennis.Scheduling;

/// <summary>Players who partner each other in more than one round (the rounds are indexes into the schedule).</summary>
public sealed record RepeatPair(PlayerPair Pair, IReadOnlyList<int> Rounds);
