namespace SocialTennis.Players;

/// <summary>A player read from the import text.</summary>
public sealed record ImportedPlayer(string FirstName, string LastName, int? Rank);
