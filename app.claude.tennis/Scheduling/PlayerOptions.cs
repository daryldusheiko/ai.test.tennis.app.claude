namespace SocialTennis.Scheduling;

/// <summary>What the press-and-hold dialog says and offers for a player.</summary>
public sealed record PlayerOptions(string Message, bool CanPin, bool CanUnpin, bool CanSwap)
{
    public bool HasOptions => CanPin || CanUnpin || CanSwap;
}
