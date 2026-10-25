namespace SocialTennis.Scheduling;

/// <summary>One game. Team 1 is the single player in a three-player game; singles has one player a side.</summary>
public sealed record Game(IReadOnlyList<Guid> Team1, IReadOnlyList<Guid> Team2, bool IsComplete = false);
