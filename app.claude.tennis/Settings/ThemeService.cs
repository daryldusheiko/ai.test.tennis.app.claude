using Microsoft.JSInterop;
using SocialTennis.Storage;

namespace SocialTennis.Settings;

/// <summary>
/// Remembers the theme on the phone and applies it. <c>index.html</c> reads the same stored value before the page is drawn,
/// so the wrong theme never flashes as the app opens.
/// </summary>
public sealed class ThemeService(IKeyValueStorage storage, IJSRuntime js)
{
    internal const string ThemeKey = "socialTennis.theme";

    public async Task<ThemeChoice> GetAsync() => Parse(await storage.GetAsync(ThemeKey));

    public async Task SetAsync(ThemeChoice choice)
    {
        await storage.SetAsync(ThemeKey, Name(choice));
        await js.InvokeVoidAsync("socialTennis.applyTheme", Name(choice));
    }

    /// <summary>Anything unknown (or nothing stored) means <see cref="ThemeChoice.Device"/>.</summary>
    public static ThemeChoice Parse(string? text) => text switch
    {
        "light" => ThemeChoice.Light,
        "dark" => ThemeChoice.Dark,
        _ => ThemeChoice.Device,
    };

    public static string Name(ThemeChoice choice) => choice switch
    {
        ThemeChoice.Light => "light",
        ThemeChoice.Dark => "dark",
        ThemeChoice.Device => "device",
        _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "Unknown theme."),
    };
}
