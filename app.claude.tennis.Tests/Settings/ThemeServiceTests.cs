using Microsoft.JSInterop;
using SocialTennis.Settings;
using SocialTennis.Tests.Players;

namespace SocialTennis.Tests.Settings;

public class ThemeServiceTests
{
    [TestCase(null, ThemeChoice.Device)]
    [TestCase("", ThemeChoice.Device)]
    [TestCase("device", ThemeChoice.Device)]
    [TestCase("light", ThemeChoice.Light)]
    [TestCase("dark", ThemeChoice.Dark)]
    [TestCase("purple", ThemeChoice.Device)]
    public void Parse_StoredText_GivesTheChoice(string? text, ThemeChoice expected) =>
        Assert.That(ThemeService.Parse(text), Is.EqualTo(expected));

    [Test]
    public void Name_EveryChoice_RoundTripsThroughParse()
    {
        foreach (var choice in Enum.GetValues<ThemeChoice>())
        {
            Assert.That(ThemeService.Parse(ThemeService.Name(choice)), Is.EqualTo(choice));
        }
    }

    [Test]
    public async Task GetAsync_NothingStored_FollowsTheDevice() =>
        Assert.That(await new ThemeService(new InMemoryStorage(), new RecordingJs()).GetAsync(), Is.EqualTo(ThemeChoice.Device));

    [Test]
    public async Task SetAsync_Dark_IsStoredAndAppliedAndReadBack()
    {
        var storage = new InMemoryStorage();
        var js = new RecordingJs();

        await new ThemeService(storage, js).SetAsync(ThemeChoice.Dark);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(storage.Values[ThemeService.ThemeKey], Is.EqualTo("dark"));
            Assert.That(js.Calls, Is.EqualTo([("socialTennis.applyTheme", new object?[] { "dark" })]));
            Assert.That(await new ThemeService(storage, js).GetAsync(), Is.EqualTo(ThemeChoice.Dark));
        }
    }

    /// <summary>A hand-written fake of the browser: it only records which calls were made.</summary>
    private sealed class RecordingJs : IJSRuntime
    {
        public List<(string Identifier, object?[] Arguments)> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add((identifier, args ?? []));
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
