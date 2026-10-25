namespace SocialTennis.Scheduling;

/// <summary>The outcome of reading the courts text box: the courts in ascending order, or the message to show.</summary>
public sealed record CourtParseResult(IReadOnlyList<int> Courts, string? Error);
