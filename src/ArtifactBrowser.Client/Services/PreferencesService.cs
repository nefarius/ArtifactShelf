using System.Text.Json;

namespace ArtifactBrowser.Client.Services;

public enum ViewMode { Details, Grid, Icons }

public enum SortField { Name, Size, Modified, Type }

public enum ItemSize { Small, Medium, Large }

public sealed class BrowserPreferences
{
    public ViewMode ViewMode { get; set; } = ViewMode.Details;

    public SortField SortField { get; set; } = SortField.Name;

    public bool SortDescending { get; set; }

    public ItemSize ItemSize { get; set; } = ItemSize.Medium;

    public bool ShowSidebar { get; set; } = true;

    public bool ShowIndexMarkdown { get; set; } = true;
}

/// <summary>Owns display preferences for the browsing UI and persists them to localStorage.</summary>
public sealed class PreferencesService(LocalStorageService storage)
{
    private const string StorageKey = "artifact-browser.preferences";

    public BrowserPreferences Preferences { get; private set; } = new();

    public event Action? Changed;

    public async Task LoadAsync()
    {
        var json = await storage.GetItemAsync(StorageKey);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<BrowserPreferences>(json);
                if (loaded is not null)
                {
                    Preferences = loaded;
                }
            }
            catch (JsonException)
            {
                // Corrupt/incompatible stored preferences; fall back to defaults.
            }
        }

        Changed?.Invoke();
    }

    public async Task UpdateAsync(Action<BrowserPreferences> mutate)
    {
        mutate(Preferences);
        Changed?.Invoke();
        await storage.SetItemAsync(StorageKey, JsonSerializer.Serialize(Preferences));
    }
}
