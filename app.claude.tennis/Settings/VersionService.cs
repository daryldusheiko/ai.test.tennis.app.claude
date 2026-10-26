using System.Net.Http.Json;
using System.Text.Json;

namespace SocialTennis.Settings;

/// <summary>Reads <c>version.json</c> from the site. It is a precached file, so it is also there offline.</summary>
public sealed class VersionService(HttpClient http)
{
    public const string FileName = "version.json";

    public async Task<VersionInfo> GetAsync()
    {
        try
        {
            var info = await http.GetFromJsonAsync(FileName, VersionJsonContext.Default.VersionInfo);
            return info is { Version.Length: > 0 } ? info with { Commit = info.Commit ?? string.Empty, Released = info.Released ?? string.Empty } : VersionInfo.Development;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
        {
            // The About page must open even when the version file can't be read.
            return VersionInfo.Development;
        }
    }
}
