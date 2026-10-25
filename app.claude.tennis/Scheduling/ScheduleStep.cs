namespace SocialTennis.Scheduling;

/// <summary>Where a schedule is in its life, so the app resumes at the same step after being closed.</summary>
public enum ScheduleStep
{
    Courts,
    Players,
    Review,
    InPlay,
}
