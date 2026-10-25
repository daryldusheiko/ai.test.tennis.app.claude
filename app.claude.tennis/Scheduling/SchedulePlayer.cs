namespace SocialTennis.Scheduling;

/// <summary>A player taking part, copied into the schedule so it survives edits to the player list.</summary>
public sealed record SchedulePlayer(Guid Id, string FirstName, string LastName, int? Rank)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
}
