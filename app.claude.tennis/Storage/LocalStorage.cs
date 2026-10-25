using Microsoft.JSInterop;

namespace SocialTennis.Storage;

/// <summary>Browser localStorage, which stays on the phone when the app is closed and when it is updated.</summary>
public sealed class LocalStorage(IJSRuntime js) : IKeyValueStorage
{
    public async Task<string?> GetAsync(string key) => await js.InvokeAsync<string?>("localStorage.getItem", key);

    public async Task SetAsync(string key, string value) => await js.InvokeVoidAsync("localStorage.setItem", key, value);
}
