using Microsoft.JSInterop;

namespace ArtifactBrowser.Client.Services;

/// <summary>Thin wrapper around browser localStorage via a JS module, with a lazy import.</summary>
public sealed class LocalStorageService(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/interop.js").AsTask());

    public async Task<string?> GetItemAsync(string key)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<string?>("getItem", key);
    }

    public async Task SetItemAsync(string key, string value)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("setItem", key, value);
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}
