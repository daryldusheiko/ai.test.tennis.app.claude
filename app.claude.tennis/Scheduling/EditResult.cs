namespace SocialTennis.Scheduling;

/// <summary>The outcome of an edit: the schedule to keep (unchanged when the edit wasn't allowed) and the message to show.</summary>
public sealed record EditResult(Schedule Schedule, string Message, bool Succeeded);
