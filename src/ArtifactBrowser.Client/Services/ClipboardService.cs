using Microsoft.JSInterop;

namespace ArtifactBrowser.Client.Services;

/// <summary>A viewport point returned from JS interop (camelCase x/y).</summary>
public sealed class DomPoint
{
    public double X { get; set; }

    public double Y { get; set; }
}

/// <summary>Copies text to the clipboard via the shared JS module.</summary>
public sealed class ClipboardService(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/interop.js").AsTask());

    public async Task CopyTextAsync(string text)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("copyText", text);
    }

    public async Task<DomPoint> GetActiveElementRectAsync()
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<DomPoint>("getActiveElementRect");
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
