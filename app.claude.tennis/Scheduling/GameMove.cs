namespace SocialTennis.Scheduling;

/// <summary>A game dragged from one position to another within its round.</summary>
public sealed record GameMove(int Round, int From, int To);
