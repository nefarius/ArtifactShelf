using System.Net.Http.Json;
using System.Text.Json;
using ArtifactBrowser.Client.Models;

namespace ArtifactBrowser.Client.Services;

/// <summary>Typed client for the server's read-only file browsing API.</summary>
public sealed class FilesApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DirectoryListingDto?> ListDirectoryAsync(string path, CancellationToken ct = default)
    {
        var url = $"api/files/list?path={Uri.EscapeDataString(path)}";
        return await http.GetFromJsonAsync<DirectoryListingDto>(url, JsonOptions, ct);
    }

    public async Task<TreeNodeDto?> GetTreeAsync(string path, CancellationToken ct = default)
    {
        var url = $"api/files/tree?path={Uri.EscapeDataString(path)}";
        return await http.GetFromJsonAsync<TreeNodeDto>(url, JsonOptions, ct);
    }

    public async Task<SearchResponseDto?> SearchAsync(string path, string query, bool recursive, CancellationToken ct = default)
    {
        var url = $"api/files/search?path={Uri.EscapeDataString(path)}&q={Uri.EscapeDataString(query)}&recursive={recursive}";
        return await http.GetFromJsonAsync<SearchResponseDto>(url, JsonOptions, ct);
    }

    public async Task<PreviewDto?> GetPreviewAsync(string path, CancellationToken ct = default)
    {
        var url = $"api/files/preview?path={Uri.EscapeDataString(path)}";
        return await http.GetFromJsonAsync<PreviewDto>(url, JsonOptions, ct);
    }

    public string GetRawUrl(string path, bool download = false) =>
        $"api/files/raw?path={Uri.EscapeDataString(path)}{(download ? "&download=true" : string.Empty)}";

    public string GetThumbnailUrl(string path) =>
        $"api/files/thumbnail?path={Uri.EscapeDataString(path)}";

    public string GetArchiveUrl() => "api/files/archive";
}
