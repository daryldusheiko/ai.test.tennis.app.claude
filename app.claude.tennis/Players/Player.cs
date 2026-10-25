namespace SocialTennis.Players;

/// <summary>A player in the list. A <see langword="null"/> rank means "None".</summary>
public sealed record Player(Guid Id, string FirstName, string LastName, int? Rank, string Comment)
{
    public const int MinRank = 1;

    public const int MaxRank = 3;

    public string FullName => $"{FirstName} {LastName}".Trim();
}
