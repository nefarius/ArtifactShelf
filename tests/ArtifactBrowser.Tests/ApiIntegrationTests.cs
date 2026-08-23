using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ArtifactBrowser.Client.Models;
using ArtifactBrowser.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ArtifactBrowser.Tests;

public sealed class ApiIntegrationTests : IDisposable
{
    private readonly TempContentRoot _root = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ArtifactBrowser:ContentRoot"] = _root.ContentRoot,
                    ["ArtifactBrowser:CacheRoot"] = _root.CacheRoot,
                });
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("healthy", payload.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_WhenReservedPathArtifactExists_StillReturnsHealthPayload()
    {
        var response = await _client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", body);
        Assert.DoesNotContain("collision-health", body);
    }

    [Fact]
    public async Task Raw_WhenReservedPathArtifactExists_StillServesQueryPath()
    {
        var response = await _client.GetAsync("/api/files/raw?path=docs/notes.txt");

        response.EnsureSuccessStatusCode();
        Assert.Equal("hello world\n", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task List_RootPath_ReturnsExpectedEntries()
    {
        var listing = await _client.GetFromJsonAsync<DirectoryListingDto>("/api/files/list?path=");

        Assert.NotNull(listing);
        Assert.Contains(listing!.Entries, e => e.Name == "docs");
        Assert.DoesNotContain(listing.Entries, e => e.Name.StartsWith('.'));
    }

    [Fact]
    public async Task List_TraversalAttempt_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/files/list?path=..%2f..%2f..%2fetc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_UnknownPath_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/files/list?path=does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Preview_Markdown_ReturnsMarkdownKind()
    {
        var preview = await _client.GetFromJsonAsync<PreviewDto>("/api/files/preview?path=README.md");

        Assert.NotNull(preview);
        Assert.Equal(PreviewKind.Markdown, preview!.Kind);
    }

    [Fact]
    public async Task Raw_SupportsRangeRequests()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/files/raw?path=docs/notes.txt");
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 3);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(4, response.Content.Headers.ContentLength);
        Assert.Equal(0, response.Content.Headers.ContentRange?.From);
        Assert.Equal(3, response.Content.Headers.ContentRange?.To);

        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(Encoding.UTF8.GetBytes("hell"), body);
    }

    [Fact]
    public async Task Raw_MissingFile_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/files/raw?path=does-not-exist.txt");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Raw_HiddenFile_ReturnsFileBytes()
    {
        var response = await _client.GetAsync("/api/files/raw?path=.hidden-file.txt");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("should not be listed\n", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PrettyUrl_ExistingFile_ReturnsFileBytes()
    {
        var response = await _client.GetAsync("/docs/notes.txt");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("hello world\n", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PrettyUrl_ExistingFile_HeadReturnsHeadersWithoutBody()
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, "/docs/notes.txt");
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Encoding.UTF8.GetByteCount("hello world\n"), response.Content.Headers.ContentLength);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task PrettyUrl_ExistingFile_SupportsRangeRequests()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/docs/notes.txt");
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 3);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(4, response.Content.Headers.ContentLength);
        Assert.Equal(0, response.Content.Headers.ContentRange?.From);
        Assert.Equal(3, response.Content.Headers.ContentRange?.To);

        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(Encoding.UTF8.GetBytes("hell"), body);
    }

    [Fact]
    public async Task PrettyUrl_Directory_DoesNotServeFileBytes()
    {
        var response = await _client.GetAsync("/docs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Artifact Browser", body);
        Assert.Contains("blazor.web.js", body);
        Assert.NotEqual("hello world\n", body);
    }

    [Fact]
    public async Task PrettyUrl_NestedFile_ReturnsFileBytes()
    {
        var response = await _client.GetAsync("/builds/v1/build.log");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("build ok\n", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PrettyUrl_HiddenFile_ReturnsFileBytes()
    {
        var response = await _client.GetAsync("/.hidden-file.txt");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("should not be listed\n", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PrettyUrl_FileInsideHiddenDirectory_ReturnsFileBytes()
    {
        var response = await _client.GetAsync("/.hidden-dir/secret.txt");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("hidden\n", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task List_RootPath_OmitsHiddenFileAndDirectory()
    {
        var listing = await _client.GetFromJsonAsync<DirectoryListingDto>("/api/files/list?path=");

        Assert.NotNull(listing);
        Assert.DoesNotContain(listing!.Entries, e => e.Name == ".hidden-file.txt");
        Assert.DoesNotContain(listing.Entries, e => e.Name == ".hidden-dir");
    }

    [Fact]
    public async Task Search_FindsNestedFile()
    {
        var result = await _client.GetFromJsonAsync<SearchResponseDto>("/api/files/search?path=&q=build.log&recursive=true");

        Assert.NotNull(result);
        Assert.Contains(result!.Results, r => r.Path == "builds/v1/build.log");
    }

    [Fact]
    public async Task Search_OmittingRecursive_DoesNotDescend()
    {
        var result = await _client.GetFromJsonAsync<SearchResponseDto>("/api/files/search?path=&q=build.log");

        Assert.NotNull(result);
        Assert.DoesNotContain(result!.Results, r => r.Path == "builds/v1/build.log");
    }

    [Fact]
    public async Task Archive_NullPaths_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/files/archive", new { paths = (string[]?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Archive_DownloadsZipOfSelectedPaths()
    {
        var response = await _client.PostAsJsonAsync("/api/files/archive", new { paths = new[] { "README.md" } });

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var readme = Assert.Single(archive.Entries, e => e.Name == "README.md");
        using var reader = new StreamReader(readme.Open());
        Assert.Equal("# Title\n\nSome **text**.\n", await reader.ReadToEndAsync());
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _root.Dispose();
    }
}
