namespace SocialTennis.Storage;

/// <summary>Text storage on the device. Behind an interface so logic can be tested without a browser.</summary>
public interface IKeyValueStorage
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);
}
