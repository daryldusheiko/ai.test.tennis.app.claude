using SocialTennis.Storage;

namespace SocialTennis.Tests.Players;

/// <summary>A hand-written fake of device storage.</summary>
internal sealed class InMemoryStorage : IKeyValueStorage
{
    public Dictionary<string, string> Values { get; } = [];

    public Task<string?> GetAsync(string key) => Task.FromResult(Values.GetValueOrDefault(key));

    public Task SetAsync(string key, string value)
    {
        Values[key] = value;
        return Task.CompletedTask;
    }
}
