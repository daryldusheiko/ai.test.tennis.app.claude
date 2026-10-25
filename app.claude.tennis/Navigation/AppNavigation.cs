namespace SocialTennis.Navigation;

/// <summary>The toolbar items and the header label for each address. Plain logic so it can be tested without a browser.</summary>
public static class AppNavigation
{
    public const string NotFoundTitle = "Not found";

    public static IReadOnlyList<NavigationItem> Items { get; } =
    [
        new("Play", string.Empty, NavigationIcon.Play),
        new("Players", "players", NavigationIcon.Players),
        new("Schedule", "schedule", NavigationIcon.Schedule),
        new("About", "about", NavigationIcon.About),
    ];

    /// <summary>
    /// Finds the toolbar item that owns an address. Sub-pages (for example players/add) belong to their section.
    /// </summary>
    /// <param name="relativePath">The address relative to the base address, with no query string or fragment.</param>
    public static NavigationItem? Find(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        var path = relativePath.Trim('/');
        var section = path.Split('/', 2)[0];
        return Items.FirstOrDefault(item => string.Equals(item.Href, path, StringComparison.OrdinalIgnoreCase)
            || (item.Href.Length > 0 && string.Equals(item.Href, section, StringComparison.OrdinalIgnoreCase)));
    }

    public static string TitleFor(string relativePath) => Find(relativePath)?.Label ?? NotFoundTitle;

    /// <summary>Strips the base address, query string and fragment from a full address.</summary>
    public static string RelativePath(string baseUri, string uri)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(uri);

        var path = uri.StartsWith(baseUri, StringComparison.OrdinalIgnoreCase) ? uri[baseUri.Length..] : uri;
        var end = path.IndexOfAny(['?', '#']);
        return end < 0 ? path : path[..end];
    }
}
