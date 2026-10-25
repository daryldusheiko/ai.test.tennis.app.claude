using SocialTennis.Scheduling;

namespace SocialTennis.Tests.Scheduling;

internal static class TestPlayers
{
    /// <summary>Players P0.. with a mix of ranks (1, 2, 3 and none) so the rank rule is exercised.</summary>
    public static List<SchedulePlayer> Mixed(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new SchedulePlayer(Guid.NewGuid(), $"P{i}", "Test", i % 4 == 3 ? null : (i % 3) + 1))];

    public static List<SchedulePlayer> WithRanks(params int?[] ranks) =>
        [.. ranks.Select((rank, i) => new SchedulePlayer(Guid.NewGuid(), $"R{i}", "Test", rank))];
}
