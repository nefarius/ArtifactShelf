using System.Net.Http.Json;
using System.Text.Json;
using ArtifactBrowser.Client.Models;

namespace ArtifactBrowser.Client.Services;

/// <summary>Loads resolved server branding once for the browsing UI.</summary>
public sealed class AppConfigService(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string HeaderTitle { get; private set; } = UiConfigDto.DefaultBrandTitle;

    public string DocumentTitle { get; private set; } = UiConfigDto.DefaultBrandTitle;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            var config = await http.GetFromJsonAsync<UiConfigDto>("api/config", JsonOptions, ct);
            if (config is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(config.HeaderTitle))
            {
                HeaderTitle = config.HeaderTitle.Trim();
            }

            if (!string.IsNullOrWhiteSpace(config.DocumentTitle))
            {
                DocumentTitle = config.DocumentTitle.Trim();
            }
        }
        catch (Exception)
        {
            // Keep built-in defaults if the config endpoint is unavailable.
        }
    }
}
