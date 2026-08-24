using ArtifactBrowser.Client.Utilities;
using Microsoft.JSInterop;

namespace ArtifactBrowser.Client.Services;

/// <summary>Owns the page UI scale and persists it to localStorage so it can be applied before WASM starts.</summary>
public sealed class ZoomService(IJSRuntime jsRuntime) : IAsyncDisposable
{
    public const string StorageKey = "artifact-browser.ui-scale";

    private readonly Lazy<Task<IJSObjectReference>> _moduleTask = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/interop.js").AsTask());

    public int Percent { get; private set; } = UiScale.DefaultPercent;

    public event Action? Changed;

    public async Task LoadAsync()
    {
        var module = await _moduleTask.Value;
        var raw = await module.InvokeAsync<string?>("getItem", StorageKey);
        Percent = UiScale.Parse(raw);
        await ApplyAsync(module);
        Changed?.Invoke();
    }

    public Task IncreaseAsync() => SetAsync(UiScale.Increase(Percent));

    public Task DecreaseAsync() => SetAsync(UiScale.Decrease(Percent));

    public Task ResetAsync() => SetAsync(UiScale.Reset());

    private async Task SetAsync(int percent)
    {
        if (percent == Percent)
        {
            return;
        }

        Percent = percent;
        var module = await _moduleTask.Value;
        await ApplyAsync(module);
        Changed?.Invoke();
    }

    private async Task ApplyAsync(IJSObjectReference module)
    {
        await module.InvokeVoidAsync("applyUiScale", UiScale.ToCssMultiplier(Percent));
        await module.InvokeVoidAsync("setItem", StorageKey, Percent.ToString());
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
