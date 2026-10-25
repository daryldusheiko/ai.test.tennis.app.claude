namespace SocialTennis.Navigation;

/// <summary>One entry in the bottom toolbar. <paramref name="Href"/> is relative to the app's base address.</summary>
public sealed record NavigationItem(string Label, string Href, NavigationIcon Icon);
