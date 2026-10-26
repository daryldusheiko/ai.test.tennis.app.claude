namespace SocialTennis.Scheduling;

/// <summary>Two players, in a fixed order so the same pair always compares equal.</summary>
public sealed record PlayerPair(Guid First, Guid Second)
{
    public static PlayerPair Of(Guid a, Guid b) => a.CompareTo(b) <= 0 ? new PlayerPair(a, b) : new PlayerPair(b, a);

    public bool Contains(Guid id) => First == id || Second == id;
}
