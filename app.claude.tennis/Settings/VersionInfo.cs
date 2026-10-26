using System.Globalization;

namespace SocialTennis.Settings;

/// <summary>
/// The version of the running app, read from <c>version.json</c> (see "About page" in spec.md). The deploy writes the real values;
/// a local build keeps the fixed development file, which has no release date.
/// </summary>
/// <param name="Version">For example "1.0.42".</param>
/// <param name="Commit">The short commit the build was made from.</param>
/// <param name="Released">The release date as yyyy-MM-dd, or empty for a local build.</param>
public sealed record VersionInfo(string Version, string Commit, string Released)
{
    public const string DevelopmentVersion = "1.0.0-dev";

    public static VersionInfo Development { get; } = new(DevelopmentVersion, "local", string.Empty);

    public string VersionText => string.IsNullOrWhiteSpace(Commit) ? $"Version {Version}" : $"Version {Version} ({Commit})";

    /// <summary>"Released 26 October 2026", or "Local build" when there is no (readable) release date.</summary>
    public string ReleasedText =>
        DateOnly.TryParseExact(Released, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? $"Released {date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}"
            : "Local build";
}
