using System.Text.Json;
using Microsoft.JSInterop;

namespace ArtifactBrowser.Client.Services;

/// <summary>Triggers a browser download of a POST-streamed ZIP archive via a JS fetch+blob helper.</summary>
public sealed class DownloadService(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/interop.js").AsTask());

    public async Task DownloadArchiveAsync(string url, IReadOnlyList<string> paths, string filename = "artifacts.zip")
    {
        var module = await _moduleTask.Value;
        var body = JsonSerializer.Serialize(new { paths });
        await module.InvokeVoidAsync("downloadViaPost", url, body, filename);
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
