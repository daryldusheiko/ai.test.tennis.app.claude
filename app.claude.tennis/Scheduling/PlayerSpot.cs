namespace SocialTennis.Scheduling;

/// <summary>A player in one particular round (index into the schedule's rounds).</summary>
public sealed record PlayerSpot(int Round, Guid Player);
